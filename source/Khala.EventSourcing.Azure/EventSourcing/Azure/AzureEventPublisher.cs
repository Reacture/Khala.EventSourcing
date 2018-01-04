namespace Khala.EventSourcing.Azure
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Messaging;
    using Microsoft.WindowsAzure.Storage;
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

            string pendingPartition = PendingEventTableEntity.GetPartitionKey(typeof(T), sourceId);
            return Flush(pendingPartition, cancellationToken);
        }

        private async Task Flush(
            string pendingPartition,
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

            string persistentPartition = firstEvent.PersistentPartition;

            List<EventTableEntity> persistentEvents = await
                GetPersistentEvents(persistentPartition, firstEvent.Version, cancellationToken).ConfigureAwait(false);

            var persistentVersions = new HashSet<int>(persistentEvents.Select(e => e.Version));

            var envelopes =
                from e in pendingEvents
                where persistentVersions.Contains(e.Version)
                let domainEvent = _serializer.Deserialize(e.EventJson)
                select new Envelope(e.MessageId, domainEvent, e.OperationId, e.CorrelationId, e.Contributor);

            await _messageBus.Send(envelopes, cancellationToken).ConfigureAwait(false);
        }

        private async Task<List<EventTableEntity>> GetPersistentEvents(
            string persistentPartition,
            int version,
            CancellationToken cancellationToken)
        {
            var query = new TableQuery<EventTableEntity>();

            string filter = CombineFilters(
                GenerateFilterCondition(
                    nameof(ITableEntity.PartitionKey),
                    Equal,
                    persistentPartition),
                And,
                GenerateFilterCondition(
                    nameof(ITableEntity.RowKey),
                    GreaterThanOrEqual,
                    EventTableEntity.GetRowKey(version)));

            return new List<EventTableEntity>(await _eventTable
                .ExecuteQuery(query.Where(filter), cancellationToken)
                .ConfigureAwait(false));
        }

        private async Task DeletePendingEvents(
            List<PendingEventTableEntity> pendingEvents,
            CancellationToken cancellationToken)
        {
            foreach (PendingEventTableEntity pendingEvent in pendingEvents)
            {
                try
                {
                    var operation = TableOperation.Delete(pendingEvent);
                    await _eventTable.Execute(operation, cancellationToken).ConfigureAwait(false);
                }
                catch (StorageException exception) when (ReasonIsNotFound(exception))
                {
                }
            }
        }

        private static bool ReasonIsNotFound(StorageException exception)
        {
            if (exception.InnerException.GetType().FullName == "System.Net.WebException")
            {
                dynamic innerException = exception.InnerException;
                if (innerException.Response.GetType().FullName == "System.Net.HttpWebResponse")
                {
                    dynamic response = innerException.Response;
                    object statusCode = response.StatusCode;
                    return (int)statusCode == 404;
                }
            }

            return false;
        }

        public async void EnqueueAll(CancellationToken cancellationToken)
            => await FlushAllPendingEvents(cancellationToken).ConfigureAwait(false);

        [EditorBrowsable(EditorBrowsableState.Never)]
        public async Task FlushAllPendingEvents(CancellationToken cancellationToken)
        {
            var filter = PendingEventTableEntity.ScanFilter;
            var query = new TableQuery<PendingEventTableEntity>().Where(filter);
            TableContinuationToken continuation = null;

            do
            {
                TableQuerySegment<PendingEventTableEntity> segment = await _eventTable
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
