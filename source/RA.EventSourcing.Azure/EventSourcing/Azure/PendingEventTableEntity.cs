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

        public int Version { get; set; }

        public string EnvelopeJson { get; set; }

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

        public static PendingEventTableEntity FromEnvelope<T>(
            Envelope envelope,
            IMessageSerializer serializer)
            where T : class, IEventSourced
        {
            if (envelope == null)
            {
                throw new ArgumentNullException(nameof(envelope));
            }

            var domainEvent = envelope.Message as IDomainEvent;

            if (domainEvent == null)
            {
                throw new ArgumentException(
                    $"{nameof(envelope)}.{nameof(envelope.Message)} must be an {nameof(IDomainEvent)}.",
                    nameof(envelope));
            }

            return new PendingEventTableEntity
            {
                PartitionKey = GetPartitionKey(typeof(T), domainEvent.SourceId),
                RowKey = GetRowKey(domainEvent.Version),
                PersistedPartition = EventTableEntity.GetPartitionKey(typeof(T), domainEvent.SourceId),
                Version = domainEvent.Version,
                EnvelopeJson = serializer.Serialize(envelope),
            };
        }
    }
}
