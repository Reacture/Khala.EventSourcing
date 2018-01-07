namespace Khala.EventSourcing.Azure
{
    using System;
    using Khala.Messaging;
    using Microsoft.WindowsAzure.Storage.Table;

    public class PersistentEvent : TableEntity
    {
        public int Version { get; set; }

        public string EventType { get; set; }

        public DateTimeOffset RaisedAt { get; set; }

        public Guid MessageId { get; set; }

        public string EventJson { get; set; }

        public Guid? OperationId { get; set; }

        public Guid? CorrelationId { get; set; }

        public string Contributor { get; set; }

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

            return $"{sourceType.Name}-{sourceId:n}";
        }

        public static string GetRowKey(int version) => $"{version:D10}";

        public static PersistentEvent Create(
            Type sourceType,
            Envelope<IDomainEvent> envelope,
            IMessageSerializer serializer)
        {
            if (sourceType == null)
            {
                throw new ArgumentNullException(nameof(sourceType));
            }

            if (envelope == null)
            {
                throw new ArgumentNullException(nameof(envelope));
            }

            if (serializer == null)
            {
                throw new ArgumentNullException(nameof(serializer));
            }

            return new PersistentEvent
            {
                PartitionKey = GetPartitionKey(sourceType, envelope.Message.SourceId),
                RowKey = GetRowKey(envelope.Message.Version),
                Version = envelope.Message.Version,
                EventType = envelope.Message.GetType().FullName,
                RaisedAt = envelope.Message.RaisedAt,
                MessageId = envelope.MessageId,
                EventJson = serializer.Serialize(envelope.Message),
                OperationId = envelope.OperationId,
                CorrelationId = envelope.CorrelationId,
                Contributor = envelope.Contributor,
            };
        }
    }
}
