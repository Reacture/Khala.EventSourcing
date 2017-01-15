namespace ReactiveArchitecture.EventSourcing.Sql
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    public interface ISqlEventStore
    {
        Task SaveEvents<T>(
            IEnumerable<IDomainEvent> events,
            CancellationToken cancellationToken)
            where T : class, IEventSourced;

        Task<IEnumerable<IDomainEvent>> LoadEvents<T>(
            Guid sourceId,
            int afterVersion,
            CancellationToken cancellationToken)
            where T : class, IEventSourced;

        Task<Guid?> FindIdByUniqueIndexedProperty<T>(
            string name,
            string value,
            CancellationToken cancellationToken)
            where T : class, IEventSourced;
    }
}
