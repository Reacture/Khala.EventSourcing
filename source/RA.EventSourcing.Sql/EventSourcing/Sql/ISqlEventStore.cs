namespace ReactiveArchitecture.EventSourcing.Sql
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public interface ISqlEventStore
    {
        Task SaveEvents<T>(IEnumerable<IDomainEvent> events)
            where T : class, IEventSourced;

        Task<IEnumerable<IDomainEvent>> LoadEvents<T>(
            Guid sourceId, int afterVersion = default(int))
            where T : class, IEventSourced;

        Task<Guid?> FindIdByUniqueIndexedProperty<T>(string name, string value)
            where T : class, IEventSourced;
    }
}
