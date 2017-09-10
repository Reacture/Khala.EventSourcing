namespace Khala.EventSourcing.Sql
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    public static class SqlEventSourcingExtensions
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1004:GenericMethodsShouldProvideTypeParameter", Justification = "As designed.")]
        public static Task SaveEvents<T>(
            this ISqlEventStore eventStore,
            IEnumerable<IDomainEvent> events,
            CancellationToken cancellationToken)
            where T : class, IEventSourced
        {
            if (eventStore == null)
            {
                throw new ArgumentNullException(nameof(eventStore));
            }

            if (events == null)
            {
                throw new ArgumentNullException(nameof(events));
            }

            return eventStore.SaveEvents<T>(events, null, cancellationToken);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1004:GenericMethodsShouldProvideTypeParameter", Justification = "As designed.")]
        public static Task SaveEvents<T>(
            this ISqlEventStore eventStore,
            IEnumerable<IDomainEvent> events,
            Guid? correlationId)
            where T : class, IEventSourced
        {
            if (eventStore == null)
            {
                throw new ArgumentNullException(nameof(eventStore));
            }

            if (events == null)
            {
                throw new ArgumentNullException(nameof(events));
            }

            return eventStore.SaveEvents<T>(events, correlationId, CancellationToken.None);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1004:GenericMethodsShouldProvideTypeParameter", Justification = "As designed.")]
        public static Task SaveEvents<T>(
            this ISqlEventStore eventStore,
            IEnumerable<IDomainEvent> events)
            where T : class, IEventSourced
        {
            if (eventStore == null)
            {
                throw new ArgumentNullException(nameof(eventStore));
            }

            if (events == null)
            {
                throw new ArgumentNullException(nameof(events));
            }

            return eventStore.SaveEvents<T>(events, null, CancellationToken.None);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1004:GenericMethodsShouldProvideTypeParameter", Justification = "As designed.")]
        public static Task<IEnumerable<IDomainEvent>> LoadEvents<T>(
            this ISqlEventStore eventStore,
            Guid sourceId,
            CancellationToken cancellationToken)
            where T : class, IEventSourced
        {
            if (eventStore == null)
            {
                throw new ArgumentNullException(nameof(eventStore));
            }

            if (sourceId == Guid.Empty)
            {
                throw new ArgumentException("Value cannot be empty.", nameof(sourceId));
            }

            return eventStore.LoadEvents<T>(sourceId, default(int), cancellationToken);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1004:GenericMethodsShouldProvideTypeParameter", Justification = "As designed.")]
        public static Task<IEnumerable<IDomainEvent>> LoadEvents<T>(
            this ISqlEventStore eventStore,
            Guid sourceId,
            int afterVersion)
            where T : class, IEventSourced
        {
            if (eventStore == null)
            {
                throw new ArgumentNullException(nameof(eventStore));
            }

            if (sourceId == Guid.Empty)
            {
                throw new ArgumentException("Value cannot be empty.", nameof(sourceId));
            }

            return eventStore.LoadEvents<T>(sourceId, afterVersion, CancellationToken.None);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1004:GenericMethodsShouldProvideTypeParameter", Justification = "As designed.")]
        public static Task<IEnumerable<IDomainEvent>> LoadEvents<T>(
            this ISqlEventStore eventStore,
            Guid sourceId)
            where T : class, IEventSourced
        {
            if (eventStore == null)
            {
                throw new ArgumentNullException(nameof(eventStore));
            }

            if (sourceId == Guid.Empty)
            {
                throw new ArgumentException("Value cannot be empty.", nameof(sourceId));
            }

            return eventStore.LoadEvents<T>(sourceId, default(int), CancellationToken.None);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1004:GenericMethodsShouldProvideTypeParameter", Justification = "As designed.")]
        public static Task<Guid?> FindIdByUniqueIndexedProperty<T>(
            this ISqlEventStore eventStore,
            string name,
            string value)
            where T : class, IEventSourced
        {
            if (eventStore == null)
            {
                throw new ArgumentNullException(nameof(eventStore));
            }

            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            return eventStore.FindIdByUniqueIndexedProperty<T>(name, value, CancellationToken.None);
        }
    }
}
