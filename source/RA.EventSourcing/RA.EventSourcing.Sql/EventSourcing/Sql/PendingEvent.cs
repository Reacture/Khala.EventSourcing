namespace ReactiveArchitecture.EventSourcing.Sql
{
    using System;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;

    public class PendingEvent
    {
        [Key]
        [Column(Order = 0)]
        public Guid AggregateId { get; set; }

        [Key]
        [Column(Order = 1)]
        public int Version { get; set; }

        [Required]
        public string PayloadJson { get; set; }
    }
}
