namespace ReactiveArchitecture.EventSourcing.Azure
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Messaging;
    using Microsoft.WindowsAzure.Storage.Table;
    using static Microsoft.WindowsAzure.Storage.Table.QueryComparisons;
    using static Microsoft.WindowsAzure.Storage.Table.TableOperators;
    using static Microsoft.WindowsAzure.Storage.Table.TableQuery;

    public class AzureEventCorrector : IAzureEventCorrector
    {
        private readonly CloudTable _eventTable;
        private readonly JsonMessageSerializer _serializer;
        private readonly IMessageBus _messageBus;

        public AzureEventCorrector(
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

        public Task CorrectEvents<T>(
            Guid sourceId,
            CancellationToken cancellationToken = default(CancellationToken))
            where T : class, IEventSourced
        {
            if (sourceId == Guid.Empty)
            {
                throw new ArgumentException(
                    $"{sourceId} cannot be empty.", nameof(sourceId));
            }

            return Correct<T>(sourceId, cancellationToken);
        }

        private async Task Correct<T>(
            Guid sourceId,
            CancellationToken cancellationToken)
            where T : class, IEventSourced
        {
            List<PendingEventTableEntity> pendingEvents =
                await GetPendingEvents<T>(sourceId, cancellationToken).ConfigureAwait(false);

            if (pendingEvents.Any())
            {
                List<IDomainEvent> domainEvents =
                    RestoreDomainEvents(pendingEvents);

                await InsertUnpersistedEvents<T>(domainEvents, cancellationToken).ConfigureAwait(false);
                await SendPendingEvents(domainEvents, cancellationToken).ConfigureAwait(false);
                await DeletePendingEvents(pendingEvents, cancellationToken).ConfigureAwait(false);
            }
        }

        private async Task<List<PendingEventTableEntity>> GetPendingEvents<T>(
            Guid sourceId,
            CancellationToken cancellationToken)
            where T : class, IEventSourced
        {
            var query = new TableQuery<PendingEventTableEntity>();

            string filter = GenerateFilterCondition(
                nameof(ITableEntity.PartitionKey),
                Equal,
                PendingEventTableEntity.GetPartitionKey(typeof(T), sourceId));

            return new List<PendingEventTableEntity>(await
                ExecuteQuery(query.Where(filter), cancellationToken).ConfigureAwait(false));
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

        private async Task<List<EventTableEntity>> GetPersistedEvents<T>(
            Guid sourceId,
            int version,
            CancellationToken cancellationToken)
            where T : class, IEventSourced
        {
            var query = new TableQuery<EventTableEntity>();

            string filter = CombineFilters(
                GenerateFilterCondition(
                    nameof(ITableEntity.PartitionKey),
                    Equal,
                    EventTableEntity.GetPartitionKey(typeof(T), sourceId)),
                And,
                GenerateFilterCondition(
                    nameof(ITableEntity.RowKey),
                    GreaterThanOrEqual,
                    EventTableEntity.GetRowKey(version)));

            return new List<EventTableEntity>(await
                ExecuteQuery(query.Where(filter), cancellationToken).ConfigureAwait(false));
        }

        private async Task InsertUnpersistedEvents<T>(
            List<IDomainEvent> domainEvents,
            CancellationToken cancellationToken)
            where T : class, IEventSourced
        {
            IDomainEvent firstEvent = domainEvents.First();
            List<EventTableEntity> persistedEvents = await
                GetPersistedEvents<T>(firstEvent.SourceId, firstEvent.Version, cancellationToken);

            IEnumerable<IDomainEvent> unpersistedEvents =
                domainEvents.Skip(persistedEvents.Count);

            var batch = new TableBatchOperation();

            foreach (IDomainEvent @event in unpersistedEvents)
            {
                var entity =
                    EventTableEntity.FromDomainEvent<T>(@event, _serializer);
                batch.Insert(entity);
            }

            await _eventTable.ExecuteBatchAsync(batch, cancellationToken).ConfigureAwait(false);
        }

        private async Task SendPendingEvents(
            List<IDomainEvent> domainEvents,
            CancellationToken cancellationToken)
        {
            await _messageBus.SendBatch(domainEvents, cancellationToken).ConfigureAwait(false);
        }

        private async Task DeletePendingEvents(
            List<PendingEventTableEntity> pendingEvents,
            CancellationToken cancellationToken)
        {
            var batch = new TableBatchOperation();
            pendingEvents.ForEach(batch.Delete);
            await _eventTable.ExecuteBatchAsync(batch, cancellationToken).ConfigureAwait(false);
        }

        private async Task<IEnumerable<TEntity>> ExecuteQuery<TEntity>(
            TableQuery<TEntity> query,
            CancellationToken cancellationToken)
            where TEntity : ITableEntity, new()
        {
            var entities = new List<TEntity>();
            TableContinuationToken continuation = null;

            do
            {
                TableQuerySegment<TEntity> segment = await _eventTable
                    .ExecuteQuerySegmentedAsync(query, continuation, cancellationToken)
                    .ConfigureAwait(false);
                entities.AddRange(segment);
                continuation = segment.ContinuationToken;
            }
            while (continuation != null);

            return entities;
        }
    }
}
