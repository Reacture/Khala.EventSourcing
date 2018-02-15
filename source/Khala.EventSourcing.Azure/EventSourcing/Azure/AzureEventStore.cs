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

    public class AzureEventStore : IAzureEventStore
    {
        private readonly CloudTable _eventTable;
        private readonly IMessageSerializer _serializer;

        public AzureEventStore(CloudTable eventTable, IMessageSerializer serializer)
        {
            _eventTable = eventTable ?? throw new ArgumentNullException(nameof(eventTable));
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        }

        public Task SaveEvents<T>(
            IEnumerable<IDomainEvent> events,
            Guid? operationId = default,
            Guid? correlationId = default,
            string contributor = default,
            CancellationToken cancellationToken = default)
            where T : class, IEventSourced
        {
            if (events == null)
            {
                throw new ArgumentNullException(nameof(events));
            }

            var domainEvents = events.ToList();

            if (domainEvents.Count == 0)
            {
                return Task.FromResult(true);
            }

            IDomainEvent firstEvent = domainEvents.First();

            for (int i = 0; i < domainEvents.Count; i++)
            {
                IDomainEvent domainEvent = domainEvents[i];

                if (domainEvent == null)
                {
                    throw new ArgumentException(
                        $"{nameof(events)} cannot contain null.",
                        nameof(events));
                }

                if (domainEvent.Version != firstEvent.Version + i)
                {
                    throw new ArgumentException(
                        $"Versions of {nameof(events)} must be sequential.",
                        nameof(events));
                }

                if (domainEvent.SourceId != firstEvent.SourceId)
                {
                    throw new ArgumentException(
                        $"All events must have the same source id.",
                        nameof(events));
                }

                if (domainEvent.RaisedAt.Kind != DateTimeKind.Utc)
                {
                    throw new ArgumentException(
                        $"RaisedAt of all events must be of kind UTC.",
                        nameof(events));
                }
            }

            return Save<T>(domainEvents, operationId, correlationId, contributor, cancellationToken);
        }

        private async Task Save<T>(
            List<IDomainEvent> domainEvents,
            Guid? operationId,
            Guid? correlationId,
            string contributor,
            CancellationToken cancellationToken)
            where T : class, IEventSourced
        {
            var envelopes = new List<Envelope<IDomainEvent>>(
                from domainEvent in domainEvents
                let messageId = Guid.NewGuid()
                select new Envelope<IDomainEvent>(messageId, domainEvent, operationId, correlationId, contributor));

            var batch = new TableBatchOperation();

            foreach (Envelope<IDomainEvent> envelope in envelopes)
            {
                batch.Insert(PersistentEvent.Create(typeof(T), envelope, _serializer));
                batch.Insert(PendingEvent.Create(typeof(T), envelope, _serializer));
            }

            Guid sourceId = domainEvents.First().SourceId;

            if (correlationId.HasValue)
            {
                batch.Insert(Correlation.Create(typeof(T), sourceId, correlationId.Value));
            }

            try
            {
                await _eventTable.ExecuteBatch(batch, cancellationToken).ConfigureAwait(false);
            }
            catch (StorageException exception) when (correlationId.HasValue)
            {
                string filter = Correlation.GetFilter(typeof(T), sourceId, correlationId.Value);
                var query = new TableQuery<Correlation> { FilterString = filter };
                if (await _eventTable.Any(query, cancellationToken).ConfigureAwait(false))
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
            int afterVersion = default,
            CancellationToken cancellationToken = default)
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
            string filter = PersistentEvent.GetFilter(typeof(T), sourceId, afterVersion);
            var query = new TableQuery<PersistentEvent> { FilterString = filter };

            IEnumerable<PersistentEvent> events = await _eventTable
                .ExecuteQuery(query, cancellationToken)
                .ConfigureAwait(false);

            return new List<IDomainEvent>(events
                .Select(e => e.EventJson)
                .Select(_serializer.Deserialize)
                .Cast<IDomainEvent>());
        }
    }
}
