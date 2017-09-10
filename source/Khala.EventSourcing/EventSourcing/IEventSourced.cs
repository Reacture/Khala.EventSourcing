namespace Khala.EventSourcing
{
    using System.Collections.Generic;

    /// <summary>
    /// Represents an event sourcing applied aggregate.
    /// </summary>
    public interface IEventSourced : IVersionedEntity
    {
        /// <summary>
        /// Returns the sequence of domain events raised after the aggregate is initialized or restored and remove them from the event queue.
        /// </summary>
        /// <returns>
        /// The sequence of domain events raised after the aggregate is initialized or restored.
        /// </returns>
        IEnumerable<IDomainEvent> FlushPendingEvents();
    }
}
