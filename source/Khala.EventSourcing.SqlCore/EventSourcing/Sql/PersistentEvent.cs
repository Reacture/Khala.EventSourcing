namespace Khala.EventSourcing.Sql
{
    using System;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using Khala.Messaging;

    public class PersistentEvent
    {
        public const string IndexName = "SqlEventStore_IX_AggregateId_Version";

        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long SequenceId { get; private set; }

#if NETSTANDARD2_0
#else
        [Index(IndexName, IsUnique = true, Order = 0)]
#endif
        public Guid AggregateId { get; set; }

#if NETSTANDARD2_0
#else
        [Index(IndexName, IsUnique = true, Order = 1)]
#endif
        public int Version { get; set; }

        [Required]
        public string EventType { get; set; }

        public Guid MessageId { get; set; }

        [Required]
        public string EventJson { get; set; }

        public Guid? OperationId { get; set; }

        public Guid? CorrelationId { get; set; }

        [StringLength(128)]
        public string Contributor { get; set; }

        public DateTimeOffset RaisedAt { get; set; }

        public static PersistentEvent FromEnvelope(
            Envelope envelope,
            IMessageSerializer serializer)
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

            return new PersistentEvent
            {
                AggregateId = domainEvent.SourceId,
                Version = domainEvent.Version,
                EventType = domainEvent.GetType().FullName,
                MessageId = envelope.MessageId,
                CorrelationId = envelope.CorrelationId,
                Contributor = envelope.Contributor,
                EventJson = serializer.Serialize(domainEvent),
                RaisedAt = domainEvent.RaisedAt
            };
        }
    }
}
