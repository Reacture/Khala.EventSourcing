namespace ReactiveArchitecture.EventSourcing.Azure
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using EventSourcing;
    using Messaging;
    using Microsoft.WindowsAzure.Storage.Table;

    public class AzureEventStore : IEventStore
    {
        private CloudTable _eventTable;
        private JsonMessageSerializer _serializer;

        public AzureEventStore(
            CloudTable eventTable, JsonMessageSerializer serializer)
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

        public Task SaveEvents<T>(IEnumerable<IDomainEvent> events)
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

            return Save<T>(domainEvents);
        }

        private async Task Save<T>(List<IDomainEvent> domainEvents)
            where T : class, IEventSourced
        {
            await InsertPendingEvents<T>(domainEvents);
            await InsertEvents<T>(domainEvents);
        }

        private async Task InsertPendingEvents<T>(List<IDomainEvent> domainEvents)
            where T : class, IEventSourced
        {
            var batch = new TableBatchOperation();

            foreach (IDomainEvent e in domainEvents)
            {
                batch.Insert(PendingEventTableEntity.FromDomainEvent<T>(e, _serializer));
            }

            await _eventTable.ExecuteBatchAsync(batch);
        }

        private async Task InsertEvents<T>(List<IDomainEvent> domainEvents)
            where T : class, IEventSourced
        {
            var batch = new TableBatchOperation();

            foreach (IDomainEvent e in domainEvents)
            {
                batch.Insert(EventTableEntity.FromDomainEvent<T>(e, _serializer));
            }

            await _eventTable.ExecuteBatchAsync(batch);
        }

        public Task<IEnumerable<IDomainEvent>> LoadEvents<T>(
            Guid sourceId, int afterVersion = default(int))
            where T : class, IEventSourced
        {
            if (sourceId == Guid.Empty)
            {
                throw new ArgumentException(
                    $"{sourceId} cannot be empty", nameof(sourceId));
            }

            throw new NotImplementedException();
        }
    }
}
