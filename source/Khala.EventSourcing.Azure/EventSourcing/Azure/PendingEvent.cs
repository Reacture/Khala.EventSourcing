namespace Khala.EventSourcing.Azure
{
    using System;
    using Khala.Messaging;
    using Microsoft.WindowsAzure.Storage.Table;

    public class PendingEvent : EventEntity
    {
        public static readonly string FullScanFilter = TableQuery.CombineFilters(
            TableQuery.GenerateFilterCondition(
                nameof(RowKey),
                QueryComparisons.GreaterThan,
                GetRowKey(version: 0)),
            TableOperators.And,
            TableQuery.GenerateFilterCondition(
                nameof(RowKey),
                QueryComparisons.LessThanOrEqual,
                GetRowKey(version: int.MaxValue)));

        public static string GetRowKey(int version) => $"Pending-{version:D10}";

        public static PendingEvent Create(
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

            return new PendingEvent
            {
                PartitionKey = GetPartitionKey(sourceType, envelope.Message.SourceId),
                RowKey = GetRowKey(envelope.Message.Version),
                MessageId = envelope.MessageId,
                EventJson = serializer.Serialize(envelope.Message),
                OperationId = envelope.OperationId,
                CorrelationId = envelope.CorrelationId,
                Contributor = envelope.Contributor,
            };
        }

        public static string GetFilter(Type sourceType, Guid sourceId)
        {
            if (sourceType == null)
            {
                throw new ArgumentNullException(nameof(sourceType));
            }

            if (sourceId == Guid.Empty)
            {
                throw new ArgumentException("Value cannot be empty.", nameof(sourceId));
            }

            return GetFilter(GetPartitionKey(sourceType, sourceId));
        }

        public static string GetFilter(string partition)
        {
            string partitionCondition = TableQuery.GenerateFilterCondition(
                nameof(PartitionKey),
                QueryComparisons.Equal,
                partition);

            string rowCondition = TableQuery.CombineFilters(
                TableQuery.GenerateFilterCondition(
                    nameof(RowKey),
                    QueryComparisons.GreaterThan,
                    GetRowKey(version: 0)),
                TableOperators.And,
                TableQuery.GenerateFilterCondition(
                    nameof(RowKey),
                    QueryComparisons.LessThanOrEqual,
                    GetRowKey(version: int.MaxValue)));

            return TableQuery.CombineFilters(partitionCondition, TableOperators.And, rowCondition);
        }
    }
}
