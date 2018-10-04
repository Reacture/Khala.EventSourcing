namespace Khala.EventSourcing.Azure
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Khala.Messaging;
    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.Storage.Table;

    public class AzureEventPublisher : IAzureEventPublisher
    {
        private readonly CloudTable _eventTable;
        private readonly IMessageSerializer _serializer;
        private readonly IMessageBus _messageBus;

        public AzureEventPublisher(
            CloudTable eventTable,
            IMessageSerializer serializer,
            IMessageBus messageBus)
        {
            _eventTable = eventTable ?? throw new ArgumentNullException(nameof(eventTable));
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            _messageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));
        }

        public Task FlushPendingEvents<T>(
            Guid sourceId,
            CancellationToken cancellationToken = default)
            where T : class, IEventSourced
        {
            if (sourceId == Guid.Empty)
            {
                throw new ArgumentException(
                    $"{sourceId} cannot be empty.", nameof(sourceId));
            }

            string partition = AggregateEntity.GetPartitionKey(typeof(T), sourceId);
            return Flush(partition, cancellationToken);
        }

        private async Task Flush(string partition, CancellationToken cancellationToken)
        {
            List<PendingEvent> pendingEvents = await GetPendingEvents(partition, cancellationToken).ConfigureAwait(false);
            if (pendingEvents.Any())
            {
                await SendPendingEvents(pendingEvents, cancellationToken).ConfigureAwait(false);
                await DeletePendingEvents(pendingEvents, cancellationToken).ConfigureAwait(false);
            }
        }

        private async Task<List<PendingEvent>> GetPendingEvents(string partition, CancellationToken cancellationToken)
        {
            string filter = PendingEvent.GetFilter(partition);
            var query = new TableQuery<PendingEvent> { FilterString = filter };
            return new List<PendingEvent>(await _eventTable
                .ExecuteQuery(query, cancellationToken)
                .ConfigureAwait(false));
        }

        private Task SendPendingEvents(List<PendingEvent> pendingEvents, CancellationToken cancellationToken)
        {
            IEnumerable<Envelope> envelopes =
                from e in pendingEvents
                let domainEvent = _serializer.Deserialize(e.EventJson)
                select new Envelope(
                    e.MessageId,
                    domainEvent,
                    e.OperationId,
                    e.CorrelationId,
                    e.Contributor);

            return _messageBus.Send(envelopes, cancellationToken);
        }

        private async Task DeletePendingEvents(List<PendingEvent> pendingEvents, CancellationToken cancellationToken)
        {
            foreach (PendingEvent pendingEvent in pendingEvents)
            {
                try
                {
                    var operation = TableOperation.Delete(pendingEvent);
                    await _eventTable.Execute(operation, cancellationToken).ConfigureAwait(false);
                }
                catch (StorageException exception) when (exception.Message == "Not Found")
                {
                }
            }
        }

        public async void EnqueueAll(CancellationToken cancellationToken)
            => await FlushAllPendingEvents(cancellationToken).ConfigureAwait(false);

        [EditorBrowsable(EditorBrowsableState.Never)]
        public async Task FlushAllPendingEvents(CancellationToken cancellationToken)
        {
            string filter = PendingEvent.FullScanFilter;
            var query = new TableQuery<PendingEvent> { FilterString = filter };
            TableContinuationToken continuation = null;

            do
            {
                TableQuerySegment<PendingEvent> segment = await _eventTable
                    .ExecuteQuerySegmented(query, continuation, cancellationToken)
                    .ConfigureAwait(false);

                foreach (string partition in segment.Select(e => e.PartitionKey).Distinct())
                {
                    await Flush(partition, cancellationToken).ConfigureAwait(false);
                }

                continuation = segment.ContinuationToken;
            }
            while (continuation != null);
        }
    }
}
