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

    public class AzureEventPublisher :
        IAzureEventPublisher, IAzureEventCorrector
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
            List<PendingEventTableEntity> pendingEvents = await
                GetPendingEvents<T>(sourceId, cancellationToken).ConfigureAwait(false);

            if (pendingEvents.Any())
            {
                List<IDomainEvent> domainEvents =
                    RestoreDomainEvents(pendingEvents);

                await SendPendingEvents(domainEvents, cancellationToken).ConfigureAwait(false);
                await DeletePendingEvents(pendingEvents, cancellationToken).ConfigureAwait(false);
            }
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

        public Task CorrectEvents<T>(
            Guid sourceId, CancellationToken cancellationToken)
            where T : class, IEventSourced
        {
            if (sourceId == Guid.Empty)
            {
                throw new ArgumentException(
                    $"{sourceId} cannot be empty.", nameof(sourceId));
            }

            string pendingPartition = PendingEventTableEntity.GetPartitionKey(typeof(T), sourceId);
            string persistedPartition = EventTableEntity.GetPartitionKey(typeof(T), sourceId);
            return Correct(pendingPartition, persistedPartition, cancellationToken);
        }

        public async void EnqueueAll(CancellationToken cancellationToken)
        {
            await AwaitEnqueueAll(cancellationToken);
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public async Task AwaitEnqueueAll(CancellationToken cancellationToken)
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
                await Correct(partition.PendingPartition, partition.PersistedPartition, cancellationToken).ConfigureAwait(false);
            }
        }

        private HashSet<T> CreateHashSet<T>(Func<T> descriptor) => new HashSet<T>();

        private async Task Correct(
            string pendingPartition,
            string persistedPartition,
            CancellationToken cancellationToken)
        {
            List<PendingEventTableEntity> pendingEvents = await
                GetPendingEvents(pendingPartition, cancellationToken).ConfigureAwait(false);

            if (pendingEvents.Any())
            {
                List<IDomainEvent> domainEvents =
                    RestoreDomainEvents(pendingEvents);

                await InsertUnpersistedEvents(persistedPartition, domainEvents, cancellationToken).ConfigureAwait(false);
                await SendPendingEvents(domainEvents, cancellationToken).ConfigureAwait(false);
                await DeletePendingEvents(pendingEvents, cancellationToken).ConfigureAwait(false);
            }
        }

        private Task<List<PendingEventTableEntity>> GetPendingEvents<T>(
            Guid sourceId,
            CancellationToken cancellationToken)
            where T : class, IEventSourced
        {
            string pendingPartition = PendingEventTableEntity.GetPartitionKey(typeof(T), sourceId);
            return GetPendingEvents(pendingPartition, cancellationToken);
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

        private async Task InsertUnpersistedEvents(
            string persistedPartition,
            List<IDomainEvent> domainEvents,
            CancellationToken cancellationToken)
        {
            IDomainEvent firstEvent = domainEvents.First();

            List<EventTableEntity> persistedEvents = await
                GetPersistedEvents(persistedPartition, firstEvent.Version, cancellationToken).ConfigureAwait(false);

            IEnumerable<IDomainEvent> unpersistedEvents =
                domainEvents.Skip(persistedEvents.Count);

            var batch = new TableBatchOperation();

            foreach (IDomainEvent @event in unpersistedEvents)
            {
                var entity = EventTableEntity.FromDomainEvent(
                    persistedPartition, @event, _serializer);
                batch.Insert(entity);
            }

            await _eventTable.ExecuteBatchAsync(batch, cancellationToken).ConfigureAwait(false);
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
    }
}
