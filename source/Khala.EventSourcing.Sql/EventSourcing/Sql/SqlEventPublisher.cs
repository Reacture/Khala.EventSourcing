namespace Khala.EventSourcing.Sql
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
        private readonly IMessageSerializer _serializer;
        private readonly IMessageBus _messageBus;

        public SqlEventPublisher(
            Func<EventStoreDbContext> dbContextFactory,
            IMessageSerializer serializer,
            IMessageBus messageBus)
        {
            _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            _messageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));
        }

        public Task PublishPendingEvents(
            Guid sourceId,
            CancellationToken cancellationToken)
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

                List<Envelope> envelopes = pendingEvents
                    .Select(e => RestoreEnvelope(e))
                    .ToList();

                if (envelopes.Any() == false)
                {
                    return;
                }

                await _messageBus.SendBatch(envelopes, cancellationToken).ConfigureAwait(false);

                context.PendingEvents.RemoveRange(pendingEvents);

                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        private Envelope RestoreEnvelope(PendingEvent pendingEvent) =>
            new Envelope(
                pendingEvent.MessageId,
                pendingEvent.CorrelationId,
                _serializer.Deserialize(pendingEvent.EventJson));

        public async void EnqueueAll(CancellationToken cancellationToken)
            => await PublishAllPendingEvents(cancellationToken).ConfigureAwait(false);

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
