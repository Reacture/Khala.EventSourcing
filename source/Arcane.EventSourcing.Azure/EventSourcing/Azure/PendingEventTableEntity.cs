namespace Arcane.EventSourcing.Azure
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

        public string PersistentPartition { get; set; }

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
        {
            if (sourceType == null)
            {
                throw new ArgumentNullException(nameof(sourceType));
            }

            return $"{PartitionPrefix}-{sourceType.Name}-{sourceId.ToString("n")}";
        }

        public static string GetRowKey(int version) => $"{version:D10}";

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1004:GenericMethodsShouldProvideTypeParameter", Justification = "As designed.")]
        public static PendingEventTableEntity FromEnvelope<T>(
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

            string persistentPartition = EventTableEntity.GetPartitionKey(
                typeof(T), domainEvent.SourceId);

            return new PendingEventTableEntity
            {
                PartitionKey = GetPartitionKey(typeof(T), domainEvent.SourceId),
                RowKey = GetRowKey(domainEvent.Version),
                PersistentPartition = persistentPartition,
                Version = domainEvent.Version,
                EnvelopeJson = serializer.Serialize(envelope),
            };
        }
    }
}
