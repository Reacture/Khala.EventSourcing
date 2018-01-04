namespace Khala.EventSourcing.Azure
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

        public Guid MessageId { get; set; }

        public string EventJson { get; set; }

        public Guid? OperationId { get; set; }

        public Guid? CorrelationId { get; set; }

        public string Contributor { get; set; }

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

            if (sourceId == Guid.Empty)
            {
                throw new ArgumentException("Value cannot be empty.", nameof(sourceId));
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
                MessageId = envelope.MessageId,
                OperationId = envelope.OperationId,
                CorrelationId = envelope.CorrelationId,
                Contributor = envelope.Contributor,
                EventJson = serializer.Serialize(domainEvent),
            };
        }
    }
}
