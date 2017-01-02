namespace ReactiveArchitecture.EventSourcing.Sql
{
    using System.Collections.Generic;

    public interface IUniqueIndexedEvent
    {
        IReadOnlyDictionary<string, string> UniqueIndexedProperties { get; }
    }
}
