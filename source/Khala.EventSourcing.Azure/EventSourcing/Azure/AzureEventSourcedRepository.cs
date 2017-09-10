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

        public Task Save(
            T source,
            Guid? correlationId,
            CancellationToken cancellationToken)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            return SaveAndPublish(source, correlationId, cancellationToken);
        }

        private async Task SaveAndPublish(
            T source, Guid? correlationId, CancellationToken cancellationToken)
        {
            await _eventStore.SaveEvents<T>(source.FlushPendingEvents(), correlationId, cancellationToken).ConfigureAwait(false);
            await _eventPublisher.PublishPendingEvents<T>(source.Id, cancellationToken).ConfigureAwait(false);

            if (_mementoStore != null)
            {
                if (source is IMementoOriginator mementoOriginator)
                {
                    IMemento memento = mementoOriginator.SaveToMemento();
                    await _mementoStore.Save<T>(source.Id, memento, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        public Task<T> Find(Guid sourceId, CancellationToken cancellationToken)
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
                .PublishPendingEvents<T>(sourceId, cancellationToken)
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
                    : default(T)
                : _mementoEntityFactory.Invoke(sourceId, memento, domainEvents);
        }
    }
}
