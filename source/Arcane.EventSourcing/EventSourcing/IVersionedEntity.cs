namespace Arcane.EventSourcing
{
    using System;

    public interface IVersionedEntity
    {
        Guid Id { get; }

        int Version { get; }
    }
}
