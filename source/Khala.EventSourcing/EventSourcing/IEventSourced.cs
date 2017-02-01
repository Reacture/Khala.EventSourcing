namespace Khala.EventSourcing
{
    using System.Collections.Generic;

    /// <summary>
    /// Represents an event sourcing applied aggregate.
    /// </summary>
    public interface IEventSourced : IVersionedEntity
    {
        /// <summary>
        /// Gets the sequence of domain events raised after the aggregate is initialized or restored.
        /// </summary>
        /// <value>
        /// The sequence of domain events raised after the aggregate is initialized or restored.
        /// </value>
        IEnumerable<IDomainEvent> PendingEvents { get; }
    }
}
