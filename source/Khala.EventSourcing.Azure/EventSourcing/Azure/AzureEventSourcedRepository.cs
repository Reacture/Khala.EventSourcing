namespace Khala.EventSourcing.Azure
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    public class AzureEventSourcedRepository<T> : IEventSourcedRepository<T>
        where T : class, IEventSourced
    {
        private readonly IAzureEventStore _eventStore;
        private readonly IAzureEventPublisher _eventPublisher;
        private readonly IMementoStore _mementoStore;
        private readonly Func<Guid, IEnumerable<IDomainEvent>, T> _entityFactory;
        private readonly Func<Guid, IMemento, IEnumerable<IDomainEvent>, T> _mementoEntityFactory;

        public AzureEventSourcedRepository(
            IAzureEventStore eventStore,
            IAzureEventPublisher eventPublisher,
            Func<Guid, IEnumerable<IDomainEvent>, T> entityFactory)
        {
            _eventStore = eventStore ?? throw new ArgumentNullException(nameof(eventStore));
            _eventPublisher = eventPublisher ?? throw new ArgumentNullException(nameof(eventPublisher));
            _entityFactory = entityFactory ?? throw new ArgumentNullException(nameof(entityFactory));
        }

        public AzureEventSourcedRepository(
            IAzureEventStore eventStore,
            IAzureEventPublisher eventPublisher,
            IMementoStore mementoStore,
            Func<Guid, IEnumerable<IDomainEvent>, T> entityFactory,
            Func<Guid, IMemento, IEnumerable<IDomainEvent>, T> mementoEntityFactory)
            : this(eventStore, eventPublisher, entityFactory)
        {
            _mementoStore = mementoStore ?? throw new ArgumentNullException(nameof(mementoStore));
            _mementoEntityFactory = mementoEntityFactory ?? throw new ArgumentNullException(nameof(mementoEntityFactory));
        }

        public IEventPublisher EventPublisher => _eventPublisher;

        public Task SaveAndPublish(
            T source,
            Guid? operationId = default,
            Guid? correlationId = default,
            string contributor = default,
            CancellationToken cancellationToken = default)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            return RunSaveAndPublish(source, correlationId, contributor, cancellationToken);
        }

        private async Task RunSaveAndPublish(
            T source,
            Guid? correlationId,
            string contributor,
            CancellationToken cancellationToken)
        {
            await SaveEvents(source, correlationId, contributor, cancellationToken).ConfigureAwait(false);
            await FlushEvents(source, cancellationToken).ConfigureAwait(false);
            await SaveMementoIfPossible(source, cancellationToken).ConfigureAwait(false);
        }

        private Task SaveEvents(T source, Guid? correlationId, string contributor, CancellationToken cancellationToken)
            => _eventStore.SaveEvents<T>(source.FlushPendingEvents(), correlationId, contributor, cancellationToken);

        private Task FlushEvents(T source, CancellationToken cancellationToken)
            => _eventPublisher.FlushPendingEvents<T>(source.Id, cancellationToken);

        private Task SaveMementoIfPossible(T source, CancellationToken cancellationToken)
        {
            if (_mementoStore != null &&
                source is IMementoOriginator mementoOriginator)
            {
                IMemento memento = mementoOriginator.SaveToMemento();
                return _mementoStore.Save<T>(source.Id, memento, cancellationToken);
            }

            return Task.FromResult(true);
        }

        public Task<T> Find(Guid sourceId, CancellationToken cancellationToken = default)
        {
            if (sourceId == Guid.Empty)
            {
                throw new ArgumentException(
                    $"{nameof(sourceId)} cannot be empty.", nameof(sourceId));
            }

            return PublishAndRestore(sourceId, cancellationToken);
        }

        private async Task<T> PublishAndRestore(
            Guid sourceId, CancellationToken cancellationToken)
        {
            await _eventPublisher
                .FlushPendingEvents<T>(sourceId, cancellationToken)
                .ConfigureAwait(false);

            IMemento memento = null;
            if (_mementoStore != null && _mementoEntityFactory != null)
            {
                memento = await _mementoStore
                    .Find<T>(sourceId, cancellationToken)
                    .ConfigureAwait(false);
            }

            IEnumerable<IDomainEvent> domainEvents = await _eventStore
                .LoadEvents<T>(sourceId, memento?.Version ?? 0, cancellationToken)
                .ConfigureAwait(false);

            return
                memento == null
                ? domainEvents.Any()
                    ? _entityFactory.Invoke(sourceId, domainEvents)
                    : default
                : _mementoEntityFactory.Invoke(sourceId, memento, domainEvents);
        }
    }
}
