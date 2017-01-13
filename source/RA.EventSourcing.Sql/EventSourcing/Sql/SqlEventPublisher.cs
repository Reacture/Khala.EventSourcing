namespace ReactiveArchitecture.EventSourcing.Sql
{
    using System;
    using System.Collections.Generic;
    using System.Data.Entity;
    using System.Linq;
    using System.Threading.Tasks;
    using Messaging;

    public class SqlEventPublisher : ISqlEventPublisher
    {
        private readonly Func<EventStoreDbContext> _dbContextFactory;
        private readonly JsonMessageSerializer _serializer;
        private readonly IMessageBus _messageBus;

        public SqlEventPublisher(
            Func<EventStoreDbContext> dbContextFactory,
            JsonMessageSerializer serializer,
            IMessageBus messageBus)
        {
            if (dbContextFactory == null)
            {
                throw new ArgumentNullException(nameof(dbContextFactory));
            }

            if (serializer == null)
            {
                throw new ArgumentNullException(nameof(serializer));
            }

            if (messageBus == null)
            {
                throw new ArgumentNullException(nameof(messageBus));
            }

            _dbContextFactory = dbContextFactory;
            _serializer = serializer;
            _messageBus = messageBus;
        }

        public Task PublishPendingEvents<T>(Guid sourceId)
            where T : class, IEventSourced
        {
            if (sourceId == Guid.Empty)
            {
                throw new ArgumentException(
                    $"{nameof(sourceId)} cannot be empty.", nameof(sourceId));
            }

            return PublishEvents(sourceId);
        }

        private async Task PublishEvents(Guid sourceId)
        {
            using (EventStoreDbContext context = _dbContextFactory.Invoke())
            {
                List<PendingEvent> pendingEvents = await context
                    .PendingEvents
                    .Where(e => e.AggregateId == sourceId)
                    .OrderBy(e => e.Version)
                    .ToListAsync()
                    .ConfigureAwait(false);

                List<object> messages = pendingEvents
                    .Select(e => e.PayloadJson)
                    .Select(_serializer.Deserialize)
                    .ToList();

                await _messageBus.SendBatch(messages).ConfigureAwait(false);

                context.PendingEvents.RemoveRange(pendingEvents);

                await context.SaveChangesAsync().ConfigureAwait(false);
            }
        }

        public async void EnqueueAll()
        {
            using (EventStoreDbContext context = _dbContextFactory.Invoke())
            {
                foreach (Guid sourceId in await context
                    .PendingEvents
                    .Select(e => e.AggregateId)
                    .Distinct()
                    .ToListAsync()
                    .ConfigureAwait(false))
                {
                    await PublishEvents(sourceId).ConfigureAwait(false);
                }
            }
        }
    }
}
