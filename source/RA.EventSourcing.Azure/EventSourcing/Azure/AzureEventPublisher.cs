namespace ReactiveArchitecture.EventSourcing.Azure
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Messaging;
    using Microsoft.WindowsAzure.Storage.Table;
    using static Microsoft.WindowsAzure.Storage.Table.QueryComparisons;
    using static Microsoft.WindowsAzure.Storage.Table.TableQuery;

    public class AzureEventPublisher : IAzureEventPublisher
    {
        private readonly CloudTable _eventTable;
        private readonly JsonMessageSerializer _serializer;
        private readonly IMessageBus _messageBus;

        public AzureEventPublisher(
            CloudTable eventTable,
            JsonMessageSerializer serializer,
            IMessageBus messageBus)
        {
            if (eventTable == null)
            {
                throw new ArgumentNullException(nameof(eventTable));
            }

            if (serializer == null)
            {
                throw new ArgumentNullException(nameof(serializer));
            }

            if (messageBus == null)
            {
                throw new ArgumentNullException(nameof(messageBus));
            }

            _eventTable = eventTable;
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

            return Publish<T>(sourceId, cancellationToken);
        }

        private async Task Publish<T>(
            Guid sourceId, CancellationToken cancellationToken)
            where T : class, IEventSourced
        {
            string partition = PendingEventTableEntity.GetPartitionKey(typeof(T), sourceId);
            await Publish(partition, cancellationToken);
        }

        private async Task Publish(
            string partition, CancellationToken cancellationToken)
        {
            List<PendingEventTableEntity> pendingEvents = await
                GetPendingEvents(partition, cancellationToken).ConfigureAwait(false);

            if (pendingEvents.Any())
            {
                List<IDomainEvent> domainEvents =
                    RestoreDomainEvents(pendingEvents);

                await SendPendingEvents(domainEvents, cancellationToken).ConfigureAwait(false);
                await DeletePendingEvents(pendingEvents, cancellationToken).ConfigureAwait(false);
            }
        }

        private async Task<List<PendingEventTableEntity>> GetPendingEvents(
            string partition, CancellationToken cancellationToken)
        {
            var query = new TableQuery<PendingEventTableEntity>();

            string filter = GenerateFilterCondition(
                nameof(ITableEntity.PartitionKey),
                Equal,
                partition);

            return new List<PendingEventTableEntity>(await _eventTable
                .ExecuteQuery(query.Where(filter), cancellationToken)
                .ConfigureAwait(false));
        }

        private List<IDomainEvent> RestoreDomainEvents(
            List<PendingEventTableEntity> pendingEvents)
        {
            return pendingEvents
                .Select(e => e.PayloadJson)
                .Select(_serializer.Deserialize)
                .Cast<IDomainEvent>()
                .ToList();
        }

        private Task SendPendingEvents(
            List<IDomainEvent> domainEvents,
            CancellationToken cancellationToken)
        {
            return _messageBus.SendBatch(domainEvents, cancellationToken);
        }

        private Task DeletePendingEvents(
            List<PendingEventTableEntity> pendingEvents,
            CancellationToken cancellationToken)
        {
            var batch = new TableBatchOperation();
            pendingEvents.ForEach(batch.Delete);
            return _eventTable.ExecuteBatchAsync(batch, cancellationToken);
        }

        public async void EnqueueAll(CancellationToken cancellationToken)
        {
            await AwaitEnqueueAll(cancellationToken);
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public async Task AwaitEnqueueAll(CancellationToken cancellationToken)
        {
            var partitions = new HashSet<string>();
            var query = new TableQuery<PendingEventTableEntity>();
            var filter = PendingEventTableEntity.ScanFilter;
            TableContinuationToken continuation = null;

            do
            {
                TableQuerySegment<PendingEventTableEntity> segment = await _eventTable
                    .ExecuteQuerySegmentedAsync(query.Where(filter), continuation, cancellationToken)
                    .ConfigureAwait(false);

                foreach (PendingEventTableEntity entity in segment)
                {
                    partitions.Add(entity.PartitionKey);
                }

                continuation = segment.ContinuationToken;
            }
            while (continuation != null);

            foreach (string partition in partitions)
            {
                await Publish(partition, cancellationToken);
            }
        }
    }
}
