namespace ReactiveArchitecture.EventSourcing
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    public class EventSourcedRepository<T> : IEventSourcedRepository<T>
        where T : class, IEventSourced
    {
        private readonly IEventStore _eventStore;
        private readonly IEventPublisher _eventPublisher;
        private readonly Func<Guid, IEnumerable<IDomainEvent>, T> _factory;

        public EventSourcedRepository(
            IEventStore eventStore,
            IEventPublisher eventPublisher,
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

            if (factory == null)
            {
                throw new ArgumentNullException(nameof(factory));
            }

            _eventStore = eventStore;
            _eventPublisher = eventPublisher;
            _factory = factory;
        }

        public Task Save(T source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            return SaveSource(source);
        }

        private async Task SaveSource(T source)
        {
            await _eventStore.SaveEvents<T>(source.PendingEvents);
            await _eventPublisher.PublishPendingEvents<T>(source.Id);
        }

        public Task<T> Find(Guid sourceId)
        {
            if (sourceId == Guid.Empty)
            {
                throw new ArgumentException(
                    $"{nameof(sourceId)} cannot be empty.", nameof(sourceId));
            }

            return FindSource(sourceId);
        }

        private async Task<T> FindSource(Guid sourceId)
        {
            var domainEvents = new List<IDomainEvent>(
                await _eventStore.LoadEvents<T>(sourceId));

            return domainEvents.Any()
                ? _factory.Invoke(sourceId, domainEvents)
                : default(T);
        }
    }
}
