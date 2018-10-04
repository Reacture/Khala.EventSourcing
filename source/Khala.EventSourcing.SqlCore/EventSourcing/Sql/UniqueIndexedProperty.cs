namespace Khala.EventSourcing.Sql
{
    using System;
    using System.ComponentModel.DataAnnotations;

    public class UniqueIndexedProperty
    {
        [StringLength(128)]
        public string AggregateType { get; set; }

        [StringLength(128)]
        public string PropertyName { get; set; }

        [StringLength(128)]
        public string PropertyValue { get; set; }

        public Guid AggregateId { get; set; }

        [ConcurrencyCheck]
        public int Version { get; set; }
    }
}
