namespace Khala.EventSourcing.Azure
{
    using System;
    using Messaging;
    using Microsoft.WindowsAzure.Storage.Table;

    public class EventTableEntity : TableEntity
    {
        public int Version { get; set; }

        public string EventType { get; set; }

        public Guid MessageId { get; set; }

        public Guid? CorrelationId { get; set; }

        public string EventJson { get; set; }

        public DateTimeOffset RaisedAt { get; set; }

        public static string GetPartitionKey(Type sourceType, Guid sourceId)
        {
            if (sourceType == null)
            {
                throw new ArgumentNullException(nameof(sourceType));
            }

            if (sourceId == Guid.Empty)
            {
                throw new ArgumentException("Value cannot be empty.", nameof(sourceId));
            }

            return $"Event-{sourceType.Name}-{sourceId.ToString("n")}";
        }

        public static string GetRowKey(int version) => $"{version:D10}";

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1004:GenericMethodsShouldProvideTypeParameter", Justification = "As designed.")]
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

            return new EventTableEntity
            {
                PartitionKey = GetPartitionKey(typeof(T), domainEvent.SourceId),
                RowKey = GetRowKey(domainEvent.Version),
                Version = domainEvent.Version,
                EventType = domainEvent.GetType().FullName,
                MessageId = envelope.MessageId,
                CorrelationId = envelope.CorrelationId,
                EventJson = serializer.Serialize(domainEvent),
                RaisedAt = domainEvent.RaisedAt
            };
        }
    }
}
