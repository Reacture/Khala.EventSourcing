namespace Khala.EventSourcing.Azure
{
    using System;
    using Khala.Messaging;

    public class PendingEvent : EventEntity
    {
        public static string GetRowKey(int version) => $"Pending-{version:D10}";

        public static PendingEvent Create(
            Type aggregateType,
            Envelope<IDomainEvent> envelope,
            IMessageSerializer serializer)
        {
            if (aggregateType == null)
            {
                throw new ArgumentNullException(nameof(aggregateType));
            }

            if (envelope == null)
            {
                throw new ArgumentNullException(nameof(envelope));
            }

            if (serializer == null)
            {
                throw new ArgumentNullException(nameof(serializer));
            }

            return new PendingEvent
            {
                PartitionKey = GetPartitionKey(aggregateType, envelope.Message.SourceId),
                RowKey = GetRowKey(envelope.Message.Version),
                MessageId = envelope.MessageId,
                EventJson = serializer.Serialize(envelope.Message),
                OperationId = envelope.OperationId,
                CorrelationId = envelope.CorrelationId,
                Contributor = envelope.Contributor,
            };
        }
    }
}
