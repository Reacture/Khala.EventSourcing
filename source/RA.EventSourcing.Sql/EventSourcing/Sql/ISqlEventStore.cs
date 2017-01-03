namespace ReactiveArchitecture.EventSourcing.Sql
{
    using System;
    using System.Threading.Tasks;

    public interface ISqlEventStore : IEventStore
    {
        Task<Guid?> FindIdByUniqueIndexedProperty<T>(string name, string value)
            where T : class, IEventSourced;
    }
}
