namespace Khala.EventSourcing.Sql
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Messaging;

    public class SqlEventSourcedRepository<T> : ISqlEventSourcedRepository<T>
        where T : class, IEventSourced
    {
        private readonly ISqlEventStore _eventStore;
        private readonly ISqlEventPublisher _eventPublisher;
        private readonly IMementoStore _mementoStore;
        private readonly Func<Guid, IEnumerable<IDomainEvent>, T> _entityFactory;
        private readonly Func<Guid, IMemento, IEnumerable<IDomainEvent>, T> _mementoEntityFactory;

        public SqlEventSourcedRepository(
            ISqlEventStore eventStore,
            ISqlEventPublisher eventPublisher,
            Func<Guid, IEnumerable<IDomainEvent>, T> entityFactory)
        {
            _eventStore = eventStore ?? throw new ArgumentNullException(nameof(eventStore));
            _eventPublisher = eventPublisher ?? throw new ArgumentNullException(nameof(eventPublisher));
            _entityFactory = entityFactory ?? throw new ArgumentNullException(nameof(entityFactory));
        }

        public SqlEventSourcedRepository(
            ISqlEventStore eventStore,
            ISqlEventPublisher eventPublisher,
            IMementoStore mementoStore,
            Func<Guid, IEnumerable<IDomainEvent>, T> entityFactory,
            Func<Guid, IMemento, IEnumerable<IDomainEvent>, T> mementoEntityFactory)
            : this(eventStore, eventPublisher, entityFactory)
        {
            _mementoStore = mementoStore ?? throw new ArgumentNullException(nameof(mementoStore));
            _mementoEntityFactory = mementoEntityFactory ?? throw new ArgumentNullException(nameof(mementoEntityFactory));
        }

        public SqlEventSourcedRepository(
            Func<EventStoreDbContext> dbContextFactory,
            IMessageSerializer serializer,
            IMessageBus messageBus,
            Func<Guid, IEnumerable<IDomainEvent>, T> entityFactory)
            : this(
                  new SqlEventStore(
                      dbContextFactory,
                      serializer),
                  new SqlEventPublisher(
                      dbContextFactory,
                      serializer,
                      messageBus),
                  entityFactory)
        {
        }

        public IEventPublisher EventPublisher => _eventPublisher;

        public Task SaveAndPublish(
            T source,
            Guid? correlationId,
            CancellationToken cancellationToken)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            async Task Run()
            {
                await SaveEvents(source, correlationId, cancellationToken).ConfigureAwait(false);
                await FlushEvents(source, cancellationToken).ConfigureAwait(false);
                await SaveMementoIfPossible(source, cancellationToken).ConfigureAwait(false);
            }

            return Run();
        }

        private Task SaveEvents(T source, Guid? correlationId, CancellationToken cancellationToken)
            => _eventStore.SaveEvents<T>(source.FlushPendingEvents(), correlationId, cancellationToken);

        private Task FlushEvents(T source, CancellationToken cancellationToken)
            => _eventPublisher.PublishPendingEvents(source.Id, cancellationToken);

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

        public Task<T> Find(Guid sourceId, CancellationToken cancellationToken)
        {
            if (sourceId == Guid.Empty)
            {
                throw new ArgumentException(
                    $"{nameof(sourceId)} cannot be empty.", nameof(sourceId));
            }

            return FindSource(sourceId, cancellationToken);
        }

        private async Task<T> FindSource(
            Guid sourceId, CancellationToken cancellationToken)
        {
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

        public Task<Guid?> FindIdByUniqueIndexedProperty(
            string name, string value, CancellationToken cancellationToken)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            return _eventStore.FindIdByUniqueIndexedProperty<T>(name, value, cancellationToken);
        }
    }
}
