namespace ReactiveArchitecture.EventSourcing.Azure
{
    using System;
    using Messaging;
    using Microsoft.WindowsAzure.Storage.Table;

    public class EventTableEntity : TableEntity
    {
        public string EventType { get; set; }

        public Guid MessageId { get; set; }

        public Guid? CorrelationId { get; set; }

        public string EventJson { get; set; }

        public DateTimeOffset RaisedAt { get; set; }

        public static string GetPartitionKey(Type sourceType, Guid sourceId)
            => $"Event-{sourceType.Name}-{sourceId.ToString("n")}";

        public static string GetRowKey(int version) => $"{version:D10}";

        public static EventTableEntity FromEnvelope<T>(
            Envelope envelope,
            IMessageSerializer serializer)
            where T : class, IEventSourced
        {
            if (envelope == null)
            {
                throw new ArgumentNullException(nameof(envelope));
            }

            if (serializer == null)
            {
                throw new ArgumentNullException(nameof(serializer));
            }

            var domainEvent = envelope.Message as IDomainEvent;

            if (domainEvent == null)
            {
                throw new ArgumentException(
                    $"{nameof(envelope)}.{nameof(envelope.Message)} must be an {nameof(IDomainEvent)}.",
                    nameof(envelope));
            }

            return Create(
                GetPartitionKey(typeof(T), domainEvent.SourceId),
                envelope.MessageId,
                envelope.CorrelationId,
                domainEvent,
                serializer);
        }

        public static EventTableEntity FromEnvelope(
            string partition,
            Envelope envelope,
            IMessageSerializer serializer)
        {
            if (partition == null)
            {
                throw new ArgumentNullException(nameof(partition));
            }

            if (envelope == null)
            {
                throw new ArgumentNullException(nameof(envelope));
            }

            if (serializer == null)
            {
                throw new ArgumentNullException(nameof(serializer));
            }

            var domainEvent = envelope.Message as IDomainEvent;

            if (domainEvent == null)
            {
                throw new ArgumentException(
                    $"{nameof(envelope)}.{nameof(envelope.Message)} must be an {nameof(IDomainEvent)}.",
                    nameof(envelope));
            }

            return Create(
                partition,
                envelope.MessageId,
                envelope.CorrelationId,
                domainEvent,
                serializer);
        }

        private static EventTableEntity Create(
            string partition,
            Guid messageId,
            Guid? correlationId,
            IDomainEvent domainEvent,
            IMessageSerializer serializer)
        {
            return new EventTableEntity
            {
                PartitionKey = partition,
                RowKey = GetRowKey(domainEvent.Version),
                EventType = domainEvent.GetType().FullName,
                MessageId = messageId,
                CorrelationId = correlationId,
                EventJson = serializer.Serialize(domainEvent),
                RaisedAt = domainEvent.RaisedAt
            };
        }
    }
}
