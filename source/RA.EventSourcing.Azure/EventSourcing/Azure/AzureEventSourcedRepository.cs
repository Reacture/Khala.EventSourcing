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
        private readonly IMementoStore _mementoStore;
        private readonly IAzureEventCorrector _eventCorrector;
        private readonly Func<Guid, IEnumerable<IDomainEvent>, T> _entityFactory;
        private readonly Func<Guid, IMemento, IEnumerable<IDomainEvent>, T> _mementoEntityFactory;

        public AzureEventSourcedRepository(
            IAzureEventStore eventStore,
            IAzureEventPublisher eventPublisher,
            IAzureEventCorrector eventCorrector,
            Func<Guid, IEnumerable<IDomainEvent>, T> entityFactory)
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

            if (entityFactory == null)
            {
                throw new ArgumentNullException(nameof(entityFactory));
            }

            _eventStore = eventStore;
            _eventPublisher = eventPublisher;
            _eventCorrector = eventCorrector;
            _entityFactory = entityFactory;
        }

        public AzureEventSourcedRepository(
            IAzureEventStore eventStore,
            IAzureEventPublisher eventPublisher,
            IMementoStore mementoStore,
            IAzureEventCorrector eventCorrector,
            Func<Guid, IEnumerable<IDomainEvent>, T> entityFactory,
            Func<Guid, IMemento, IEnumerable<IDomainEvent>, T> mementoEntityFactory)
            : this(eventStore, eventPublisher, eventCorrector, entityFactory)
        {
            if (mementoStore == null)
            {
                throw new ArgumentNullException(nameof(mementoStore));
            }

            if (mementoEntityFactory == null)
            {
                throw new ArgumentNullException(nameof(mementoEntityFactory));
            }

            _mementoStore = mementoStore;
            _mementoEntityFactory = mementoEntityFactory;
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

            if (_mementoStore != null)
            {
                var mementoOriginator = source as IMementoOriginator;
                if (mementoOriginator != null)
                {
                    IMemento memento = mementoOriginator.SaveToMemento();
                    await _mementoStore.Save<T>(source.Id, memento).ConfigureAwait(false);
                }
            }
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
            IMemento memento = null;
            if (_mementoStore != null && _mementoEntityFactory != null)
            {
                memento = await _mementoStore
                    .Find<T>(sourceId)
                    .ConfigureAwait(false);
            }

            await _eventCorrector
                .CorrectEvents<T>(sourceId)
                .ConfigureAwait(false);

            IEnumerable<IDomainEvent> domainEvents = await _eventStore
                .LoadEvents<T>(sourceId, memento?.Version ?? 0)
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
