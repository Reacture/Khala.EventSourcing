namespace Khala.EventSourcing.Sql
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Represents an event store based on a relational database.
    /// </summary>
    public interface ISqlEventStore
    {
        /// <summary>
        /// Saves domain events to the relational database.
        /// </summary>
        /// <typeparam name="T">The type of the event sourcing applied aggregate.</typeparam>
        /// <param name="events">A seqeuence that contains domain events.</param>
        /// <param name="correlationId">The identifier of the correlation.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1004:GenericMethodsShouldProvideTypeParameter", Justification = "As designed.")]
        Task SaveEvents<T>(
            IEnumerable<IDomainEvent> events,
            Guid? correlationId,
            CancellationToken cancellationToken)
            where T : class, IEventSourced;

        /// <summary>
        /// Loads domain events for an event sourcing applied aggregates after the specified versiion.
        /// </summary>
        /// <typeparam name="T">The type of the event sourcing applied aggregate.</typeparam>
        /// <param name="sourceId">The identifier of the event sourcing applied aggregate.</param>
        /// <param name="afterVersion">Domain events with a version greater than this parameter are loaded. To load all domain events from the aggregate, set this parameter to zero.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A task representing the asychronous operation. The task result contains domain events.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1004:GenericMethodsShouldProvideTypeParameter", Justification = "As designed.")]
        Task<IEnumerable<IDomainEvent>> LoadEvents<T>(
            Guid sourceId,
            int afterVersion,
            CancellationToken cancellationToken)
            where T : class, IEventSourced;

        /// <summary>
        /// Finds an identifier of an event sourcing applied aggregate with the given unique indexed property.
        /// </summary>
        /// <typeparam name="T">The type of the event sourcing applied aggregate.</typeparam>
        /// <param name="name">The name of the property.</param>
        /// <param name="value">The value of the property.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A task representing asynchronous operation. The task contains the identifier found, or <c>null</c>.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1004:GenericMethodsShouldProvideTypeParameter", Justification = "As designed.")]
        Task<Guid?> FindIdByUniqueIndexedProperty<T>(
            string name,
            string value,
            CancellationToken cancellationToken)
            where T : class, IEventSourced;
    }
}
