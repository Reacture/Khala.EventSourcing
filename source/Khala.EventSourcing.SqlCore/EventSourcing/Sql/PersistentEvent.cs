namespace Khala.EventSourcing.Sql
{
    using System;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using Khala.Messaging;

    public class PersistentEvent
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long SequenceId { get; private set; }

        [Required]
        [StringLength(maximumLength: 128)]
        public string AggregateType { get; set; }

        public Guid AggregateId { get; set; }

        public int Version { get; set; }

        [Required]
        public string EventType { get; set; }

        public Guid MessageId { get; set; }

        [Required]
        public string EventJson { get; set; }

        [StringLength(100)]
        public string OperationId { get; set; }

        public Guid? CorrelationId { get; set; }

        [StringLength(128)]
        public string Contributor { get; set; }

        public DateTime RaisedAt { get; set; }

        internal static PersistentEvent FromEnvelope<T>(
            Envelope envelope,
            IMessageSerializer serializer)
            where T : class, IEventSourced
        {
            var domainEvent = (IDomainEvent)envelope.Message;
            return new PersistentEvent
            {
                AggregateType = typeof(T).FullName,
                AggregateId = domainEvent.SourceId,
                Version = domainEvent.Version,
                EventType = domainEvent.GetType().FullName,
                MessageId = envelope.MessageId,
                EventJson = serializer.Serialize(domainEvent),
                OperationId = envelope.OperationId,
                CorrelationId = envelope.CorrelationId,
                Contributor = envelope.Contributor,
                RaisedAt = domainEvent.RaisedAt,
            };
        }
    }
}
