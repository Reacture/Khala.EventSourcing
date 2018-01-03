namespace Khala.EventSourcing
{
    using System.Threading;

    /// <summary>
    /// Provides interfaces to manage event publishing.
    /// </summary>
    public interface IEventPublisher
    {
        /// <summary>
        /// Starts to publish pending events for all aggregates having events not published yet.
        /// </summary>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        void EnqueueAll(CancellationToken cancellationToken = default);
    }
}
