namespace Arcane.EventSourcing.Sql
{
    using System;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;

    public class UniqueIndexedProperty
    {
        public const string IndexName = "SqlEventStore_IX_AggregateId_PropertyName";

        [Key]
        [Column(Order = 0)]
        [StringLength(128)]
        public string AggregateType { get; set; }

        [Key]
        [Column(Order = 1)]
        [StringLength(128)]
        [Index(IndexName, IsUnique = true, Order = 1)]
        public string PropertyName { get; set; }

        [Key]
        [Column(Order = 2)]
        [StringLength(256)]
        public string PropertyValue { get; set; }

        [Index(IndexName, IsUnique = true, Order = 0)]
        public Guid AggregateId { get; set; }

        [ConcurrencyCheck]
        public int Version { get; set; }
    }
}
