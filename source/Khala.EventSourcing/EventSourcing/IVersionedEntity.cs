namespace Khala.EventSourcing
{
    using System;

    /// <summary>
    /// Represents an entity having version.
    /// </summary>
    public interface IVersionedEntity
    {
        /// <summary>
        /// Gets the identifier of the entity.
        /// </summary>
        /// <value>
        /// The identifier of the entity.
        /// </value>
        Guid Id { get; }

        /// <summary>
        /// Gets the current version of the entity.
        /// </summary>
        /// <value>
        /// The current version of the entity.
        /// </value>
        int Version { get; }
    }
}
