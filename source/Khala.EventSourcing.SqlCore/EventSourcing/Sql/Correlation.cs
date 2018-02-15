namespace Khala.EventSourcing.Sql
{
    using System;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;

    public class Correlation
    {
        [Key]
        [Column(Order = 0)]
        public Guid AggregateId { get; set; }

        [Key]
        [Column(Order = 1)]
        public Guid CorrelationId { get; set; }

        public DateTime HandledAt { get; set; }
    }
}
