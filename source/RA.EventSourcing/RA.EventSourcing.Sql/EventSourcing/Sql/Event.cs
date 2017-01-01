namespace ReactiveArchitecture.EventSourcing.Sql
{
    using System;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using EventSourcing;
    using Messaging;

    public class Event
    {
        public const string IndexName = "SqlEventStore_IX_AggregateId_Version";

        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long SequenceId { get; private set; }

        [Index(IndexName, IsUnique = true, Order = 0)]
        public Guid AggregateId { get; set; }

        [Index(IndexName, IsUnique = true, Order = 1)]
        public int Version { get; set; }

        [Required]
        public string EventType { get; set; }

        [Required]
        public string PayloadJson { get; set; }

        public DateTimeOffset RaisedAt { get; set; }

        public static Event FromDomainEvent(
            IDomainEvent domainEvent, JsonMessageSerializer serializer)
        {
            if (domainEvent == null)
            {
                throw new ArgumentNullException(nameof(domainEvent));
            }

            if (serializer == null)
            {
                throw new ArgumentNullException(nameof(serializer));
            }

            return new Event
            {
                AggregateId = domainEvent.SourceId,
                Version = domainEvent.Version,
                EventType = domainEvent.GetType().FullName,
                PayloadJson = serializer.Serialize(domainEvent),
                RaisedAt = domainEvent.RaisedAt
            };
        }
    }
}
