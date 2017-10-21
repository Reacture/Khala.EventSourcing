namespace Khala.EventSourcing.Sql
{
    using System;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;

    public class Memento
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long SequenceId { get; private set; }

#if NETSTANDARD2_0
#else
        [Index(IsUnique = true)]
#endif
        public Guid AggregateId { get; set; }

        [Required]
        public string MementoJson { get; set; }
    }
}
