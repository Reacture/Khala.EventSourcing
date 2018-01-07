namespace Khala.EventSourcing.Azure
{
    using System;

    public class CorrelationEntity : AggregateEntity
    {
        public Guid CorrelationId { get; set; }

        public static string GetRowKey(Guid correlationId)
        {
            if (correlationId == Guid.Empty)
            {
                throw new ArgumentException("Value cannot be empty.", nameof(correlationId));
            }

            return $"Correlation-{correlationId:n}";
        }

        public static CorrelationEntity Create(
            Type aggregateType,
            Guid aggregateId,
            Guid correlationId)
        {
            if (aggregateType == null)
            {
                throw new ArgumentNullException(nameof(aggregateType));
            }

            if (aggregateId == Guid.Empty)
            {
                throw new ArgumentException("Value cannot be empty.", nameof(aggregateId));
            }

            if (correlationId == Guid.Empty)
            {
                throw new ArgumentException("Value cannot be empty.", nameof(correlationId));
            }

            return new CorrelationEntity
            {
                PartitionKey = GetPartitionKey(aggregateType, aggregateId),
                RowKey = GetRowKey(correlationId),
                CorrelationId = correlationId,
            };
        }
    }
}
