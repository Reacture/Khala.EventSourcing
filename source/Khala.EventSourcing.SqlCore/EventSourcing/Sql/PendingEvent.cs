namespace Khala.EventSourcing.Sql
{
    using System;
    using System.ComponentModel.DataAnnotations;
    using Khala.Messaging;

    public class PendingEvent
    {
        [Required]
        [StringLength(maximumLength: 128)]
        public string AggregateType { get; set; }

        public Guid AggregateId { get; set; }

        public int Version { get; set; }

        public Guid MessageId { get; set; }

        [Required]
        public string EventJson { get; set; }

        [StringLength(100)]
        public string OperationId { get; set; }

        public Guid? CorrelationId { get; set; }

        public string Contributor { get; set; }

        public static PendingEvent FromEnvelope<T>(
            Envelope envelope,
            IMessageSerializer serializer)
            where T : class, IEventSourced
        {
            var domainEvent = (IDomainEvent)envelope.Message;
            return new PendingEvent
            {
                AggregateType = typeof(T).FullName,
                AggregateId = domainEvent.SourceId,
                Version = domainEvent.Version,
                MessageId = envelope.MessageId,
                EventJson = serializer.Serialize(domainEvent),
                OperationId = envelope.OperationId,
                CorrelationId = envelope.CorrelationId,
                Contributor = envelope.Contributor,
            };
        }
    }
}
