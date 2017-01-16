namespace ReactiveArchitecture.EventSourcing.Sql
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Data.Entity;
    using System.Linq;
    using System.Threading;
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

        public Task PublishPendingEvents<T>(
            Guid sourceId,
            CancellationToken cancellationToken)
            where T : class, IEventSourced
        {
            if (sourceId == Guid.Empty)
            {
                throw new ArgumentException(
                    $"{nameof(sourceId)} cannot be empty.", nameof(sourceId));
            }

            return PublishEvents(sourceId, cancellationToken);
        }

        private async Task PublishEvents(
            Guid sourceId,
            CancellationToken cancellationToken)
        {
            using (EventStoreDbContext context = _dbContextFactory.Invoke())
            {
                List<PendingEvent> pendingEvents = await context
                    .PendingEvents
                    .Where(e => e.AggregateId == sourceId)
                    .OrderBy(e => e.Version)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                List<object> messages = pendingEvents
                    .Select(e => e.PayloadJson)
                    .Select(_serializer.Deserialize)
                    .ToList();

                await _messageBus.SendBatch(messages, cancellationToken).ConfigureAwait(false);

                context.PendingEvents.RemoveRange(pendingEvents);

                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        public async void EnqueueAll(CancellationToken cancellationToken)
        {
            await PublishAllPendingEvents(cancellationToken);
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public async Task PublishAllPendingEvents(CancellationToken cancellationToken)
        {
            using (EventStoreDbContext context = _dbContextFactory.Invoke())
            {
                foreach (Guid sourceId in await context
                    .PendingEvents
                    .Select(e => e.AggregateId)
                    .Distinct()
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false))
                {
                    await PublishEvents(sourceId, cancellationToken).ConfigureAwait(false);
                }
            }
        }
    }
}
