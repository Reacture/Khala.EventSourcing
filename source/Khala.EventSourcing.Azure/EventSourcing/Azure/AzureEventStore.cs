namespace Khala.EventSourcing.Azure
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Messaging;
    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.Storage.Table;

    using static Microsoft.WindowsAzure.Storage.Table.QueryComparisons;
    using static Microsoft.WindowsAzure.Storage.Table.TableOperators;
    using static Microsoft.WindowsAzure.Storage.Table.TableQuery;

    public class AzureEventStore : IAzureEventStore
    {
        private CloudTable _eventTable;
        private IMessageSerializer _serializer;

        public AzureEventStore(
            CloudTable eventTable, IMessageSerializer serializer)
        {
            _eventTable = eventTable ?? throw new ArgumentNullException(nameof(eventTable));
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        }

        public Task SaveEvents<T>(
            IEnumerable<IDomainEvent> events,
            Guid? correlationId,
            CancellationToken cancellationToken)
            where T : class, IEventSourced
        {
            if (events == null)
            {
                throw new ArgumentNullException(nameof(events));
            }

            List<IDomainEvent> domainEvents = events.ToList();

            for (int i = 0; i < domainEvents.Count; i++)
            {
                if (domainEvents[i] == null)
                {
                    throw new ArgumentException(
                        $"{nameof(events)} cannot contain null.",
                        nameof(events));
                }
            }

            return Save<T>(domainEvents, correlationId, default, cancellationToken);
        }

        public Task SaveEvents<T>(
            IEnumerable<IDomainEvent> events,
            Guid? correlationId,
            string contributor,
            CancellationToken cancellationToken)
            where T : class, IEventSourced
        {
            if (events == null)
            {
                throw new ArgumentNullException(nameof(events));
            }

            List<IDomainEvent> domainEvents = events.ToList();

            for (int i = 0; i < domainEvents.Count; i++)
            {
                if (domainEvents[i] == null)
                {
                    throw new ArgumentException(
                        $"{nameof(events)} cannot contain null.",
                        nameof(events));
                }
            }

            return Save<T>(domainEvents, correlationId, contributor, cancellationToken);
        }

        private async Task Save<T>(
            List<IDomainEvent> domainEvents,
            Guid? correlationId,
            string contributor,
            CancellationToken cancellationToken)
            where T : class, IEventSourced
        {
            if (domainEvents.Any() == false)
            {
                return;
            }

            var envelopes = new List<Envelope>(
                from domainEvent in domainEvents
                select new Envelope(Guid.NewGuid(), domainEvent, correlationId: correlationId, contributor: contributor));

            await InsertPendingEvents<T>(envelopes, cancellationToken).ConfigureAwait(false);
            await InsertEventsAndCorrelation<T>(envelopes, correlationId, cancellationToken).ConfigureAwait(false);
        }

        private Task InsertPendingEvents<T>(
            List<Envelope> envelopes,
            CancellationToken cancellationToken)
            where T : class, IEventSourced
        {
            var batch = new TableBatchOperation();

            foreach (Envelope envelope in envelopes)
            {
                batch.Insert(PendingEventTableEntity.FromEnvelope<T>(envelope, _serializer));
            }

            return _eventTable.ExecuteBatch(batch, cancellationToken);
        }

        private async Task InsertEventsAndCorrelation<T>(
            List<Envelope> envelopes,
            Guid? correlationId,
            CancellationToken cancellationToken)
            where T : class, IEventSourced
        {
            var batch = new TableBatchOperation();

            var firstEvent = (IDomainEvent)envelopes.First().Message;
            Guid sourceId = firstEvent.SourceId;

            foreach (Envelope envelope in envelopes)
            {
                batch.Insert(EventTableEntity.FromEnvelope<T>(envelope, _serializer));
            }

            if (correlationId.HasValue)
            {
                batch.Insert(CorrelationTableEntity.Create(typeof(T), sourceId, correlationId.Value));
            }

            try
            {
                await _eventTable.ExecuteBatch(batch, cancellationToken).ConfigureAwait(false);
            }
            catch (StorageException exception) when (correlationId.HasValue)
            {
                string filter = CorrelationTableEntity.GetFilter(typeof(T), sourceId, correlationId.Value);
                var query = new TableQuery<CorrelationTableEntity>().Where(filter);
                if (await _eventTable.Any(query, cancellationToken))
                {
                    throw new DuplicateCorrelationException(
                        typeof(T),
                        sourceId,
                        correlationId.Value,
                        exception);
                }

                throw;
            }
        }

        public Task<IEnumerable<IDomainEvent>> LoadEvents<T>(
            Guid sourceId,
            int afterVersion,
            CancellationToken cancellationToken)
            where T : class, IEventSourced
        {
            if (sourceId == Guid.Empty)
            {
                throw new ArgumentException(
                    $"{nameof(sourceId)} cannot be empty.", nameof(sourceId));
            }

            return Load<T>(sourceId, afterVersion, cancellationToken);
        }

        private async Task<IEnumerable<IDomainEvent>> Load<T>(
            Guid sourceId,
            int afterVersion,
            CancellationToken cancellationToken)
            where T : class, IEventSourced
        {
            string filter = CombineFilters(
                GenerateFilterCondition(
                    nameof(ITableEntity.PartitionKey),
                    Equal,
                    EventTableEntity.GetPartitionKey(typeof(T), sourceId)),
                And,
                GenerateFilterCondition(
                    nameof(ITableEntity.RowKey),
                    GreaterThan,
                    EventTableEntity.GetRowKey(afterVersion)));

            var query = new TableQuery<EventTableEntity>().Where(filter);

            IEnumerable<EventTableEntity> events = await _eventTable
                .ExecuteQuery(query, cancellationToken)
                .ConfigureAwait(false);

            return new List<IDomainEvent>(events
                .Select(e => e.EventJson)
                .Select(_serializer.Deserialize)
                .Cast<IDomainEvent>());
        }
    }
}
