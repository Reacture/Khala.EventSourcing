namespace Khala.EventSourcing
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Represents a repository that stores event sourcing applied aggregates and manages pending events.
    /// </summary>
    /// <typeparam name="T">The type of the event sourcing applied aggregate.</typeparam>
    public interface IEventSourcedRepository<T>
        where T : class, IEventSourced
    {
        /// <summary>
        /// Gets the <see cref="EventPublisher"/> that manages pending events.
        /// </summary>
        /// <value>
        /// The <see cref="EventPublisher"/> that manages pending events.
        /// </value>
        IEventPublisher EventPublisher { get; }

        /// <summary>
        /// Saves an event sourcing applied aggregate and publishes its pending events.
        /// </summary>
        /// <param name="source">An event sourcing applied aggregate.</param>
        /// <param name="correlationId">The identifier of the correlation.</param>
        /// <param name="contributor">Information of the contributor to domain events, or <c>null</c>.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task SaveAndPublish(
            T source,
            Guid? correlationId = default,
            string contributor = default,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Finds an event sourcing applied aggregate with the given identifier asynchronously.
        /// </summary>
        /// <param name="sourceId">The identifier of the entity to be found.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A task representing the asynchronous find operation. The task result contains the aggregate found, or <c>null</c>.</returns>
        Task<T> Find(Guid sourceId, CancellationToken cancellationToken = default);
    }
}
