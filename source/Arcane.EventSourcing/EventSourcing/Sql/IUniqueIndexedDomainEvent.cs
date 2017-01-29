namespace Arcane.EventSourcing.Sql
{
    using System.Collections.Generic;

    public interface IUniqueIndexedDomainEvent : IDomainEvent
    {
        IReadOnlyDictionary<string, string> UniqueIndexedProperties { get; }
    }
}
