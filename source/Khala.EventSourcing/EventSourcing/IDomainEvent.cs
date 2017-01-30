namespace Khala.EventSourcing
{
    using System;

    public interface IDomainEvent
    {
        Guid SourceId { get; }

        int Version { get; }

        DateTimeOffset RaisedAt { get; }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1030:UseEventsWhereAppropriate", Justification = "Raise() method does not follow .NET event pattern but follows event sourcing.")]
        void Raise(IVersionedEntity source);
    }
}
