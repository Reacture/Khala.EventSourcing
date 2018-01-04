namespace Khala.EventSourcing.Azure
{
    using System;
    using Microsoft.WindowsAzure.Storage.Table;
    using static Microsoft.WindowsAzure.Storage.Table.QueryComparisons;
    using static Microsoft.WindowsAzure.Storage.Table.TableOperators;
    using static Microsoft.WindowsAzure.Storage.Table.TableQuery;

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
                HandledAt = DateTimeOffset.Now,
            };
        }

        internal static string GetFilter(
            Type sourceType, Guid sourceId, Guid correlationId)
        {
            return CombineFilters(
                GenerateFilterCondition(
                    nameof(ITableEntity.PartitionKey),
                    Equal,
                    GetPartitionKey(sourceType, sourceId)),
                And,
                GenerateFilterCondition(
                    nameof(ITableEntity.RowKey),
                    Equal,
                    GetRowKey(correlationId)));
        }
    }
}
