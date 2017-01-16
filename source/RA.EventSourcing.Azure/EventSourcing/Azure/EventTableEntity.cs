namespace ReactiveArchitecture.EventSourcing.Azure
{
    using System;
    using Messaging;
    using Microsoft.WindowsAzure.Storage.Table;

    public class EventTableEntity : TableEntity
    {
        public string EventType { get; set; }

        public string PayloadJson { get; set; }

        public DateTimeOffset RaisedAt { get; set; }

        public static string GetPartitionKey(Type sourceType, Guid sourceId)
            => $"Event-{sourceType.Name}-{sourceId.ToString("n")}";

        public static string GetRowKey(int version) => $"{version:D10}";

        public static EventTableEntity FromDomainEvent<T>(
            IDomainEvent domainEvent,
            JsonMessageSerializer serializer)
            where T : class, IEventSourced
        {
            if (domainEvent == null)
            {
                throw new ArgumentNullException(nameof(domainEvent));
            }

            string partition = GetPartitionKey(typeof(T), domainEvent.SourceId);
            return FromDomainEvent(partition, domainEvent, serializer);
        }

        internal static EventTableEntity FromDomainEvent(
            string partition,
            IDomainEvent domainEvent,
            JsonMessageSerializer serializer)
        {
            return new EventTableEntity
            {
                PartitionKey = partition,
                RowKey = GetRowKey(domainEvent.Version),
                EventType = domainEvent.GetType().FullName,
                PayloadJson = serializer.Serialize(domainEvent),
                RaisedAt = domainEvent.RaisedAt
            };
        }
    }
}
