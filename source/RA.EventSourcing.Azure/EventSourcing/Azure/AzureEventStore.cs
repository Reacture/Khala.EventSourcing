﻿namespace ReactiveArchitecture.EventSourcing.Azure
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

    public class AzureEventStore : IAzureEventStore
    {
        private CloudTable _eventTable;
        private IMessageSerializer _serializer;

        public AzureEventStore(
            CloudTable eventTable, IMessageSerializer serializer)
        {
            if (eventTable == null)
            {
                throw new ArgumentNullException(nameof(eventTable));
            }

            if (serializer == null)
            {
                throw new ArgumentNullException(nameof(serializer));
            }

            _eventTable = eventTable;
            _serializer = serializer;
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

            return Save<T>(domainEvents, correlationId, cancellationToken);
        }

        private async Task Save<T>(
            List<IDomainEvent> domainEvents,
            Guid? correlationId,
            CancellationToken cancellationToken)
            where T : class, IEventSourced
        {
            if (domainEvents.Any() == false)
            {
                return;
            }

            var envelopes = new List<Envelope>(
                correlationId == null
                ? domainEvents.Select(e => new Envelope(e))
                : domainEvents.Select(e => new Envelope(correlationId.Value, e)));

            await InsertPendingEvents<T>(envelopes, cancellationToken).ConfigureAwait(false);
            await InsertEvents<T>(envelopes, cancellationToken).ConfigureAwait(false);
        }

        private async Task InsertPendingEvents<T>(
            List<Envelope> envelopes,
            CancellationToken cancellationToken)
            where T : class, IEventSourced
        {
            var batch = new TableBatchOperation();

            foreach (Envelope envelope in envelopes)
            {
                batch.Insert(PendingEventTableEntity.FromEnvelope<T>(envelope, _serializer));
            }

            await _eventTable.ExecuteBatchAsync(batch, cancellationToken).ConfigureAwait(false);
        }

        private async Task InsertEvents<T>(
            List<Envelope> envelopes,
            CancellationToken cancellationToken)
            where T : class, IEventSourced
        {
            var batch = new TableBatchOperation();

            foreach (Envelope envelope in envelopes)
            {
                batch.Insert(EventTableEntity.FromEnvelope<T>(envelope, _serializer));
            }

            await _eventTable.ExecuteBatchAsync(batch, cancellationToken).ConfigureAwait(false);
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
            var query = new TableQuery<EventTableEntity>();

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

            IEnumerable<EventTableEntity> events = await _eventTable
                .ExecuteQuery(query.Where(filter), cancellationToken)
                .ConfigureAwait(false);

            return new List<IDomainEvent>(events
                .Select(e => e.EventJson)
                .Select(_serializer.Deserialize)
                .Cast<IDomainEvent>());
        }
    }
}
