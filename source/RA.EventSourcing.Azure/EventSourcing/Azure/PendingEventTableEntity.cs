namespace ReactiveArchitecture.EventSourcing.Azure
{
    using System;
    using Messaging;
    using Microsoft.WindowsAzure.Storage.Table;

    public class PendingEventTableEntity : TableEntity
    {
        public string SourceType { get; set; }

        public Guid SourceId { get; set; }

        public string EventType { get; set; }

        public string PayloadJson { get; set; }

        public DateTimeOffset RaisedAt { get; set; }

        public static string GetPartitionKey(Type sourceType, Guid sourceId)
            => $"PendingEvent-{sourceType.Name}-{sourceId.ToString("n")}";

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
                SourceType = typeof(T).FullName,
                SourceId = domainEvent.SourceId,
                EventType = domainEvent.GetType().FullName,
                PayloadJson = serializer.Serialize(domainEvent),
                RaisedAt = domainEvent.RaisedAt
            };
        }
    }
}
