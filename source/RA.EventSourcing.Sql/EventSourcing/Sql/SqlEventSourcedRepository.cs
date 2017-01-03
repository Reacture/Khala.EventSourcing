namespace ReactiveArchitecture.EventSourcing.Sql
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public class SqlEventSourcedRepository<T> :
        EventSourcedRepository<T>,
        ISqlEventSourcedRepository<T>
        where T : class, IEventSourced
    {
        private readonly ISqlEventStore _eventStore;

        public SqlEventSourcedRepository(
            ISqlEventStore eventStore,
            IEventPublisher eventPublisher,
            Func<Guid, IEnumerable<IDomainEvent>, T> factory)
            : base(eventStore, eventPublisher, factory)
        {
            _eventStore = eventStore;
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
