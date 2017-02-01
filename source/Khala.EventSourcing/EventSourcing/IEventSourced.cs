namespace Khala.EventSourcing
{
    using System.Collections.Generic;

    /// <summary>
    /// Represents an event sourcing applied aggregate.
    /// </summary>
    public interface IEventSourced : IVersionedEntity
    {
        /// <summary>
        /// Gets the sequence of domain events raised after the aggregate is created or restored.
        /// </summary>
        /// <value>
        /// The sequence of domain events raised after the aggregate is created or restored.
        /// </value>
        IEnumerable<IDomainEvent> PendingEvents { get; }
    }
}
