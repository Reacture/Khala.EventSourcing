namespace Khala.EventSourcing.Sql
{
    using System;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;

    public class Aggregate
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long SequenceId { get; private set; }

        [Required]
        [StringLength(maximumLength: 128)]
        public string AggregateType { get; set; }

        public Guid AggregateId { get; set; }

        [ConcurrencyCheck]
        public int Version { get; set; }
    }
}
