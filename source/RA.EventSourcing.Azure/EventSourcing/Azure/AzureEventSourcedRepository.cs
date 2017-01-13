namespace ReactiveArchitecture.EventSourcing.Azure
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    public class AzureEventSourcedRepository<T> : IEventSourcedRepository<T>
        where T : class, IEventSourced
    {
        private readonly IAzureEventStore _eventStore;
        private readonly IAzureEventPublisher _eventPublisher;
        private readonly IAzureEventCorrector _eventCorrector;
        private readonly Func<Guid, IEnumerable<IDomainEvent>, T> _factory;

        public AzureEventSourcedRepository(
            IAzureEventStore eventStore,
            IAzureEventPublisher eventPublisher,
            IAzureEventCorrector eventCorrector,
            Func<Guid, IEnumerable<IDomainEvent>, T> factory)
        {
            if (eventStore == null)
            {
                throw new ArgumentNullException(nameof(eventStore));
            }

            if (eventPublisher == null)
            {
                throw new ArgumentNullException(nameof(eventPublisher));
            }

            if (eventCorrector == null)
            {
                throw new ArgumentNullException(nameof(eventCorrector));
            }

            if (factory == null)
            {
                throw new ArgumentNullException(nameof(factory));
            }

            _eventStore = eventStore;
            _eventPublisher = eventPublisher;
            _eventCorrector = eventCorrector;
            _factory = factory;
        }

        public Task Save(T source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            return SaveAndPublish(source);
        }

        private async Task SaveAndPublish(T source)
        {
            await _eventStore.SaveEvents<T>(source.PendingEvents).ConfigureAwait(false);
            await _eventPublisher.PublishPendingEvents<T>(source.Id).ConfigureAwait(false);
        }

        public Task<T> Find(Guid sourceId)
        {
            if (sourceId == Guid.Empty)
            {
                throw new ArgumentException(
                    $"{nameof(sourceId)} cannot be empty.", nameof(sourceId));
            }

            return CorrectAndRestore(sourceId);
        }

        private async Task<T> CorrectAndRestore(Guid sourceId)
        {
            await _eventCorrector
                .CorrectEvents<T>(sourceId)
                .ConfigureAwait(false);

            IEnumerable<IDomainEvent> domainEvents = await _eventStore
                .LoadEvents<T>(sourceId)
                .ConfigureAwait(false);

            return domainEvents.Any()
                ? _factory.Invoke(sourceId, domainEvents)
                : default(T);
        }
    }
}
