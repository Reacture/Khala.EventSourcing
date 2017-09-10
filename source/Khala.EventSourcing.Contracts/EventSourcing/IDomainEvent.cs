namespace Khala.EventSourcing
{
    using System;

    /// <summary>
    /// Represents a domain event.
    /// </summary>
    public interface IDomainEvent
    {
        /// <summary>
        /// Gets the identifier of the event source.
        /// </summary>
        /// <value>
        /// The identifier of the event source.
        /// </value>
        /// <remarks>This property is set by <see cref="Raise(IVersionedEntity)"/> method.</remarks>
        Guid SourceId { get; }

        /// <summary>
        /// Gets the versiion of the domain event.
        /// </summary>
        /// <value>
        /// The versiion of the domain event.
        /// </value>
        /// <remarks>This property is set by <see cref="Raise(IVersionedEntity)"/> method.</remarks>
        int Version { get; }

        /// <summary>
        /// Gets the time that the domain event is raised at.
        /// </summary>
        /// <value>
        /// The time that the domain event is raised at.
        /// </value>
        /// <remarks>This property is set by <see cref="Raise(IVersionedEntity)"/> method.</remarks>
        DateTimeOffset RaisedAt { get; }

        /// <summary>
        /// Raises the domain event with the event source.
        /// </summary>
        /// <param name="source">The event source.</param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1030:UseEventsWhereAppropriate", Justification = "Raise() method does not follow .NET event pattern but follows event sourcing.")]
        void Raise(IVersionedEntity source);
    }
}
