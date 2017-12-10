namespace Khala.EventSourcing.Sql
{
    using System;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using Khala.Messaging;

    public class PendingEvent
    {
        [Key]
        [Column(Order = 0)]
        public Guid AggregateId { get; set; }

        [Key]
        [Column(Order = 1)]
        public int Version { get; set; }

        public Guid MessageId { get; set; }

        public Guid? CorrelationId { get; set; }

        [Required]
        public string EventJson { get; set; }

        public string Contributor { get; set; }

        public static PendingEvent FromEnvelope(
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

            return new PendingEvent
            {
                AggregateId = domainEvent.SourceId,
                Version = domainEvent.Version,
                MessageId = envelope.MessageId,
                CorrelationId = envelope.CorrelationId,
                Contributor = envelope.Contributor,
                EventJson = serializer.Serialize(domainEvent)
            };
        }
    }
}
