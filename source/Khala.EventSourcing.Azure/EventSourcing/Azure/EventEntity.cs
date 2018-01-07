namespace Khala.EventSourcing.Azure
{
    using System;

    public abstract class EventEntity : AggregateEntity
    {
        public Guid MessageId { get; set; }

        public string EventJson { get; set; }

        public Guid? OperationId { get; set; }

        public Guid? CorrelationId { get; set; }

        public string Contributor { get; set; }
    }
}
