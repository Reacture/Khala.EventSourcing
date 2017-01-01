namespace ReactiveArchitecture.EventSourcing
{
    using System;

    public interface IDomainEvent
    {
        Guid SourceId { get; }

        int Version { get; }

        DateTimeOffset RaisedAt { get; }

        void Raise(IVersionedEntity source);
    }
}
