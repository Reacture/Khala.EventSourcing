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
            }

            return Save<T>(domainEvents);
        }

        private async Task Save<T>(List<IDomainEvent> events)
            where T : class, IEventSourced
        {
            using (EventStoreDbContext context = _dbContextFactory.Invoke())
            {
                await UpsertAggregate<T>(context, events);
                InsertEvents(context, events);
                await context.SaveChangesAsync();
            }
        }

        private async Task UpsertAggregate<T>(
            EventStoreDbContext context, List<IDomainEvent> events)
            where T : class, IEventSourced
        {
            IDomainEvent firstEvent = events.First();
            IDomainEvent lastEvent = events.Last();

            Aggregate aggregate = await context
                .Aggregates
                .Where(a => a.AggregateId == lastEvent.SourceId)
                .SingleOrDefaultAsync();

            if (aggregate == null)
            {
                aggregate = new Aggregate
                {
                    AggregateId = lastEvent.SourceId,
                    AggregateType = typeof(T).FullName,
                    Version = 0
                };
                context.Aggregates.Add(aggregate);
            }

            if (firstEvent.Version != aggregate.Version + 1)
            {
                throw new ArgumentException(
                    $"Version of the first of {nameof(events)} must follow aggregate.",
                    nameof(events));
            }

            aggregate.Version = lastEvent.Version;
        }

        private void InsertEvents(
            EventStoreDbContext context, List<IDomainEvent> events)
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
