namespace ReactiveArchitecture.EventSourcing.Azure
{
    using System;
    using Messaging;
    using Microsoft.WindowsAzure.Storage.Table;
    using static Microsoft.WindowsAzure.Storage.Table.QueryComparisons;
    using static Microsoft.WindowsAzure.Storage.Table.TableOperators;
    using static Microsoft.WindowsAzure.Storage.Table.TableQuery;

    public class PendingEventTableEntity : TableEntity
    {
        public const string PartitionPrefix = "PendingEvent";

        public string PersistedPartition { get; set; }

        public string EventType { get; set; }

        public string PayloadJson { get; set; }

        public DateTimeOffset RaisedAt { get; set; }

        internal static string ScanFilter =>
            CombineFilters(
                GenerateFilterCondition(
                    nameof(ITableEntity.PartitionKey),
                    GreaterThan,
                    PartitionPrefix),
                And,
                GenerateFilterCondition(
                    nameof(ITableEntity.PartitionKey),
                    LessThan,
                    $"{PartitionPrefix}."));

        public static string GetPartitionKey(Type sourceType, Guid sourceId)
            => $"{PartitionPrefix}-{sourceType.Name}-{sourceId.ToString("n")}";

        public static string GetRowKey(int version) => $"{version:D10}";

        public static PendingEventTableEntity FromDomainEvent<T>(
            IDomainEvent domainEvent, JsonMessageSerializer serializer)
            where T : class, IEventSourced
        {
            if (domainEvent == null)
            {
                throw new ArgumentNullException(nameof(domainEvent));
            }

            return new PendingEventTableEntity
            {
                PartitionKey = GetPartitionKey(typeof(T), domainEvent.SourceId),
                RowKey = GetRowKey(domainEvent.Version),
                PersistedPartition = EventTableEntity.GetPartitionKey(typeof(T), domainEvent.SourceId),
                EventType = domainEvent.GetType().FullName,
                PayloadJson = serializer.Serialize(domainEvent),
                RaisedAt = domainEvent.RaisedAt
            };
        }
    }
}
