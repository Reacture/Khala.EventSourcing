namespace Khala.EventSourcing.Azure
{
    using System;
    using Microsoft.WindowsAzure.Storage.Table;

    public class CorrelationTableEntity : TableEntity
    {
        public const string RowKeyPrefix = "Correlation";

        public Guid CorrelationId { get; set; }

        public DateTimeOffset HandledAt { get; set; }

        public static string GetPartitionKey(Type sourceType, Guid sourceId)
            => EventTableEntity.GetPartitionKey(sourceType, sourceId);

        public static string GetRowKey(Guid correlationId)
            => $"{RowKeyPrefix}-{correlationId:n}";

        internal static CorrelationTableEntity Create(
            Type sourceType, Guid sourceId, Guid correlationId)
        {
            return new CorrelationTableEntity
            {
                PartitionKey = GetPartitionKey(sourceType, sourceId),
                RowKey = GetRowKey(correlationId),
                CorrelationId = correlationId,
                HandledAt = DateTimeOffset.Now
            };
        }
    }
}
