namespace ReactiveArchitecture.EventSourcing.Sql
{
    using System;
    using System.Collections.Generic;
    using System.Data.Entity;
    using System.Linq;
    using System.Threading.Tasks;
    using Messaging;

    public class SqlEventStore : IEventStore
    {
        private readonly Func<EventStoreDbContext> _dbContextFactory;
        private readonly JsonMessageSerializer _serializer;

        public SqlEventStore(
            Func<EventStoreDbContext> dbContextFactory,
            JsonMessageSerializer serializer)
        {
            if (dbContextFactory == null)
            {
                throw new ArgumentNullException(nameof(dbContextFactory));
            }

            if (serializer == null)
            {
                throw new ArgumentNullException(nameof(serializer));
            }

            _dbContextFactory = dbContextFactory;
            _serializer = serializer;
        }

        public Task SaveEvents<T>(IEnumerable<IDomainEvent> events)
            where T : class, IEventSourced
        {
            if (events == null)
            {
                throw new ArgumentNullException(nameof(events));
            }

            List<IDomainEvent> domainEvents = events.ToList();

            if (domainEvents.Count == 0)
            {
                return Task.FromResult(true);
            }

            IDomainEvent firstEvent = domainEvents.First();

            for (int i = 0; i < domainEvents.Count; i++)
            {
                if (domainEvents[i] == null)
                {
                    throw new ArgumentException(
                        $"{nameof(events)} cannot contain null.",
                        nameof(events));
                }

                if (domainEvents[i].Version != firstEvent.Version + i)
                {
                    throw new ArgumentException(
                        $"Versions of {nameof(events)} must be sequential.",
                        nameof(events));
                }

                if (domainEvents[i].SourceId != firstEvent.SourceId)
                {
                    throw new ArgumentException(
                        $"All events must have the same source id.",
                        nameof(events));
                }
            }

            return Save<T>(firstEvent.SourceId, domainEvents);
        }

        private async Task Save<T>(Guid sourceId, List<IDomainEvent> events)
            where T : class, IEventSourced
        {
            using (EventStoreDbContext context = _dbContextFactory.Invoke())
            {
                await UpsertAggregate<T>(context, sourceId, events);
                InsertEvents(context, events);
                await UpdateUniqueIndexedProperties<T>(context, sourceId, events);
                await context.SaveChangesAsync();
            }
        }

        private async Task UpsertAggregate<T>(
            EventStoreDbContext context,
            Guid sourceId,
            List<IDomainEvent> events)
            where T : class, IEventSourced
        {
            Aggregate aggregate = await context
                .Aggregates
                .Where(a => a.AggregateId == sourceId)
                .SingleOrDefaultAsync();

            if (aggregate == null)
            {
                aggregate = new Aggregate
                {
                    AggregateId = sourceId,
                    AggregateType = typeof(T).FullName,
                    Version = 0
                };
                context.Aggregates.Add(aggregate);
            }

            if (events.First().Version != aggregate.Version + 1)
            {
                throw new ArgumentException(
                    $"Version of the first of {nameof(events)} must follow aggregate.",
                    nameof(events));
            }

            aggregate.Version = events.Last().Version;
        }

        private void InsertEvents(
            EventStoreDbContext context,
            List<IDomainEvent> events)
        {
            foreach (IDomainEvent e in events)
            {
                var @event = Event.FromDomainEvent(e, _serializer);
                context.Events.Add(@event);
                context.PendingEvents.Add(new PendingEvent
                {
                    AggregateId = @event.AggregateId,
                    Version = @event.Version,
                    PayloadJson = @event.PayloadJson
                });
            }
        }

        private async Task UpdateUniqueIndexedProperties<T>(
            EventStoreDbContext context,
            Guid sourceId,
            List<IDomainEvent> events)
            where T : class, IEventSourced
        {
            Dictionary<string, UniqueIndexedProperty> restored = await context
                .UniqueIndexedProperties
                .Where(
                    p =>
                    p.AggregateType == typeof(T).FullName &&
                    p.AggregateId == sourceId)
                .ToDictionaryAsync(p => p.PropertyName);

            var properties = new List<UniqueIndexedProperty>();

            foreach (IUniqueIndexedDomainEvent indexedEvent in
                events.OfType<IUniqueIndexedDomainEvent>())
            {
                foreach (string name in
                    indexedEvent.UniqueIndexedProperties.Keys)
                {
                    UniqueIndexedProperty property;
                    if (restored.TryGetValue(name, out property))
                    {
                        context.UniqueIndexedProperties.Remove(property);
                    }

                    property = properties.Find(p => p.PropertyName == name);
                    if (property != null)
                    {
                        properties.Remove(property);
                    }

                    string value = indexedEvent.UniqueIndexedProperties[name];
                    if (value == null)
                    {
                        continue;
                    }

                    property = new UniqueIndexedProperty
                    {
                        AggregateType = typeof(T).FullName,
                        PropertyName = name,
                        PropertyValue = value,
                        AggregateId = sourceId,
                        Version = indexedEvent.Version
                    };
                    properties.Add(property);
                }
            }

            context.UniqueIndexedProperties.AddRange(properties);
        }

        public Task<IEnumerable<IDomainEvent>> LoadEvents<T>(
            Guid sourceId, int afterVersion = default(int))
            where T : class, IEventSourced
        {
            if (sourceId == Guid.Empty)
            {
                throw new ArgumentException(
                    $"{sourceId} cannot be empty", nameof(sourceId));
            }

            return Load(sourceId, afterVersion);
        }

        private async Task<IEnumerable<IDomainEvent>> Load(
            Guid sourceId, int afterVersion)
        {
            using (EventStoreDbContext context = _dbContextFactory.Invoke())
            {
                List<Event> events = await context
                    .Events
                    .Where(e => e.AggregateId == sourceId)
                    .Where(e => e.Version > afterVersion)
                    .OrderBy(e => e.Version)
                    .ToListAsync();

                List<IDomainEvent> domainEvents = events
                    .Select(e => e.PayloadJson)
                    .Select(_serializer.Deserialize)
                    .Cast<IDomainEvent>()
                    .ToList();

                return domainEvents;
            }
        }
    }
}
