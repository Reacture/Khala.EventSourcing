namespace Khala.EventSourcing.Azure
{
    using System;
    using Microsoft.WindowsAzure.Storage.Table;

    public class Correlation : AggregateEntity
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

        public static Correlation Create(
            Type sourceType,
            Guid sourceId,
            Guid correlationId)
        {
            if (sourceType == null)
            {
                throw new ArgumentNullException(nameof(sourceType));
            }

            if (sourceId == Guid.Empty)
            {
                throw new ArgumentException("Value cannot be empty.", nameof(sourceId));
            }

            if (correlationId == Guid.Empty)
            {
                throw new ArgumentException("Value cannot be empty.", nameof(correlationId));
            }

            return new Correlation
            {
                PartitionKey = GetPartitionKey(sourceType, sourceId),
                RowKey = GetRowKey(correlationId),
                CorrelationId = correlationId,
            };
        }

        public static string GetFilter(
            Type sourceType,
            Guid sourceId,
            Guid correlationId)
        {
            if (sourceType == null)
            {
                throw new ArgumentNullException(nameof(sourceType));
            }

            if (sourceId == Guid.Empty)
            {
                throw new ArgumentException("Value cannot be empty.", nameof(sourceId));
            }

            if (correlationId == Guid.Empty)
            {
                throw new ArgumentException("Value cannot be empty.", nameof(correlationId));
            }

            string partitionCondition = TableQuery.GenerateFilterCondition(
                nameof(PartitionKey),
                QueryComparisons.Equal,
                GetPartitionKey(sourceType, sourceId));

            string rowCondition = TableQuery.GenerateFilterCondition(
                nameof(RowKey),
                QueryComparisons.Equal,
                GetRowKey(correlationId));

            return TableQuery.CombineFilters(partitionCondition, TableOperators.And, rowCondition);
        }
    }
}
