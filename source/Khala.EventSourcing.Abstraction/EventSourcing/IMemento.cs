namespace Khala.EventSourcing
{
    /// <summary>
    /// Represents a versioned snapshot of an entity.
    /// </summary>
    public interface IMemento
    {
        /// <summary>
        /// Gets the version of the snapshot.
        /// </summary>
        /// <value>
        /// The version of the snapshot.
        /// </value>
        int Version { get; }
    }
}
