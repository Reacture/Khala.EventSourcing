namespace ReactiveArchitecture.EventSourcing.Sql
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    public class SqlEventSourcedRepository<T> : ISqlEventSourcedRepository<T>
        where T : class, IEventSourced
    {
        private readonly ISqlEventStore _eventStore;
        private readonly ISqlEventPublisher _eventPublisher;
        private readonly Func<Guid, IEnumerable<IDomainEvent>, T> _factory;

        public SqlEventSourcedRepository(
            ISqlEventStore eventStore,
            ISqlEventPublisher eventPublisher,
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

        public Task<Guid?> FindIdByUniqueIndexedProperty(
            string name, string value)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            return _eventStore.FindIdByUniqueIndexedProperty<T>(name, value);
        }
    }
}
