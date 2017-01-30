namespace Khala.EventSourcing.Sql
{
    using System;
    using System.Collections.Generic;
    using System.Data.Entity;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Messaging;

    public class SqlEventStore : ISqlEventStore
    {
        private readonly Func<EventStoreDbContext> _dbContextFactory;
        private readonly IMessageSerializer _serializer;

        public SqlEventStore(
            Func<EventStoreDbContext> dbContextFactory,
            IMessageSerializer serializer)
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

        public Task SaveEvents<T>(
            IEnumerable<IDomainEvent> events,
            Guid? correlationId,
            CancellationToken cancellationToken)
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

            return Save<T>(firstEvent.SourceId, domainEvents, correlationId, cancellationToken);
        }

        private async Task Save<T>(
            Guid sourceId,
            List<IDomainEvent> events,
            Guid? correlationId,
            CancellationToken cancellationToken)
            where T : class, IEventSourced
        {
            using (EventStoreDbContext context = _dbContextFactory.Invoke())
            {
                await UpsertAggregate<T>(context, sourceId, events, cancellationToken).ConfigureAwait(false);
                InsertEvents(context, events, correlationId);
                await UpdateUniqueIndexedProperties<T>(context, sourceId, events, cancellationToken).ConfigureAwait(false);
                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "This method is extracted.")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private async Task UpsertAggregate<T>(
            EventStoreDbContext context,
            Guid sourceId,
            List<IDomainEvent> events,
            CancellationToken cancellationToken)
            where T : class, IEventSourced
        {
            Aggregate aggregate = await context
                .Aggregates
                .Where(a => a.AggregateId == sourceId)
                .SingleOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

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
            List<IDomainEvent> events,
            Guid? correlationId)
        {
            foreach (IDomainEvent domainEvent in events)
            {
                var envelope =
                    correlationId == null
                    ? new Envelope(domainEvent)
                    : new Envelope(correlationId.Value, domainEvent);
                context.PersistentEvents.Add(PersistentEvent.FromEnvelope(envelope, _serializer));
                context.PendingEvents.Add(new PendingEvent
                {
                    AggregateId = domainEvent.SourceId,
                    Version = domainEvent.Version,
                    EnvelopeJson = _serializer.Serialize(envelope)
                });
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "This method is extracted.")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private async Task UpdateUniqueIndexedProperties<T>(
            EventStoreDbContext context,
            Guid sourceId,
            List<IDomainEvent> events,
            CancellationToken cancellationToken)
            where T : class, IEventSourced
        {
            Dictionary<string, UniqueIndexedProperty> restored = await context
                .UniqueIndexedProperties
                .Where(
                    p =>
                    p.AggregateType == typeof(T).FullName &&
                    p.AggregateId == sourceId)
                .ToDictionaryAsync(p => p.PropertyName, cancellationToken)
                .ConfigureAwait(false);

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
            Guid sourceId,
            int afterVersion,
            CancellationToken cancellationToken)
            where T : class, IEventSourced
        {
            if (sourceId == Guid.Empty)
            {
                throw new ArgumentException(
                    $"{sourceId} cannot be empty", nameof(sourceId));
            }

            return Load(sourceId, afterVersion, cancellationToken);
        }

        private async Task<IEnumerable<IDomainEvent>> Load(
            Guid sourceId,
            int afterVersion,
            CancellationToken cancellationToken)
        {
            using (EventStoreDbContext context = _dbContextFactory.Invoke())
            {
                List<PersistentEvent> events = await context
                    .PersistentEvents
                    .Where(e => e.AggregateId == sourceId)
                    .Where(e => e.Version > afterVersion)
                    .OrderBy(e => e.Version)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                List<IDomainEvent> domainEvents = events
                    .Select(e => e.EventJson)
                    .Select(_serializer.Deserialize)
                    .Cast<IDomainEvent>()
                    .ToList();

                return domainEvents;
            }
        }

        public Task<Guid?> FindIdByUniqueIndexedProperty<T>(
            string name,
            string value,
            CancellationToken cancellationToken)
            where T : class, IEventSourced
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            return FindIdByProperty<T>(name, value, cancellationToken);
        }

        private async Task<Guid?> FindIdByProperty<T>(
            string name,
            string value,
            CancellationToken cancellationToken)
            where T : class, IEventSourced
        {
            using (EventStoreDbContext context = _dbContextFactory.Invoke())
            {
                IQueryable<UniqueIndexedProperty> query =
                    from p in context.UniqueIndexedProperties
                    where
                        p.AggregateType == typeof(T).FullName &&
                        p.PropertyName == name &&
                        p.PropertyValue == value
                    select p;

                UniqueIndexedProperty property = await query
                    .SingleOrDefaultAsync(cancellationToken)
                    .ConfigureAwait(false);

                return property?.AggregateId;
            }
        }
    }
}
