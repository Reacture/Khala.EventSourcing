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
    using static Microsoft.WindowsAzure.Storage.Table.TableOperators;
    using static Microsoft.WindowsAzure.Storage.Table.TableQuery;

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
                    $"{sourceId} cannot be empty.", nameof(sourceId));
            }

            string pendingPartition = PendingEventTableEntity.GetPartitionKey(typeof(T), sourceId);
            string persistedPartition = EventTableEntity.GetPartitionKey(typeof(T), sourceId);
            return Publish(pendingPartition, persistedPartition, cancellationToken);
        }

        private async Task Publish(
            string pendingPartition,
            string persistedPartition,
            CancellationToken cancellationToken)
        {
            List<PendingEventTableEntity> pendingEvents = await
                GetPendingEvents(pendingPartition, cancellationToken).ConfigureAwait(false);

            if (pendingEvents.Any())
            {
                await SendPendingEvents(pendingEvents, cancellationToken).ConfigureAwait(false);
                await DeletePendingEvents(pendingEvents, cancellationToken).ConfigureAwait(false);
            }
        }

        private async Task<List<PendingEventTableEntity>> GetPendingEvents(
            string pendingPartition,
            CancellationToken cancellationToken)
        {
            var query = new TableQuery<PendingEventTableEntity>();

            string filter = GenerateFilterCondition(
                nameof(ITableEntity.PartitionKey),
                Equal,
                pendingPartition);

            return new List<PendingEventTableEntity>(await _eventTable
                .ExecuteQuery(query.Where(filter), cancellationToken)
                .ConfigureAwait(false));
        }

        private async Task SendPendingEvents(
            List<PendingEventTableEntity> pendingEvents,
            CancellationToken cancellationToken)
        {
            PendingEventTableEntity firstEvent = pendingEvents.First();

            string persistedPartition = firstEvent.PersistedPartition;

            List<EventTableEntity> persistedEvents = await
                GetPersistedEvents(persistedPartition, firstEvent.Version, cancellationToken).ConfigureAwait(false);

            var persistedVersions = new HashSet<int>(persistedEvents.Select(e => e.Version));

            var envelopes =
                from e in pendingEvents
                where persistedVersions.Contains(e.Version)
                select (Envelope)_serializer.Deserialize(e.EnvelopeJson);
            await _messageBus.SendBatch(envelopes, cancellationToken).ConfigureAwait(false);
        }

        private async Task<List<EventTableEntity>> GetPersistedEvents(
            string persistedPartition,
            int version,
            CancellationToken cancellationToken)
        {
            var query = new TableQuery<EventTableEntity>();

            string filter = CombineFilters(
                GenerateFilterCondition(
                    nameof(ITableEntity.PartitionKey),
                    Equal,
                    persistedPartition),
                And,
                GenerateFilterCondition(
                    nameof(ITableEntity.RowKey),
                    GreaterThanOrEqual,
                    EventTableEntity.GetRowKey(version)));

            return new List<EventTableEntity>(await _eventTable
                .ExecuteQuery(query.Where(filter), cancellationToken)
                .ConfigureAwait(false));
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
            await PublishAllEvents(cancellationToken);
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public async Task PublishAllEvents(CancellationToken cancellationToken)
        {
            var partitions = CreateHashSet(() => new
            {
                PendingPartition = default(string),
                PersistedPartition = default(string)
            });

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
                    partitions.Add(new
                    {
                        PendingPartition = entity.PartitionKey,
                        PersistedPartition = entity.PersistedPartition
                    });
                }

                continuation = segment.ContinuationToken;
            }
            while (continuation != null);

            foreach (var partition in partitions)
            {
                await Publish(partition.PendingPartition, partition.PersistedPartition, cancellationToken).ConfigureAwait(false);
            }
        }

        private HashSet<T> CreateHashSet<T>(Func<T> descriptor) => new HashSet<T>();
    }
}
