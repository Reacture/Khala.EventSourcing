namespace Khala.EventSourcing.Sql
{
    using System;
    using System.ComponentModel.DataAnnotations;

    public class Correlation
    {
        [StringLength(128)]
        public string AggregateType { get; set; }

        public Guid AggregateId { get; set; }

        public Guid CorrelationId { get; set; }

        public DateTime HandledAt { get; set; }
    }
}
