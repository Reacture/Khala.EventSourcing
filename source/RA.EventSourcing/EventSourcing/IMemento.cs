namespace ReactiveArchitecture.EventSourcing
{
    using System;

    public interface IMemento
    {
        Guid SourceId { get; }

        int Version { get; }
    }
}
