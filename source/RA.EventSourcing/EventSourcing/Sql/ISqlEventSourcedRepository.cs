namespace ReactiveArchitecture.EventSourcing.Sql
{
    using System;
    using System.Threading.Tasks;

    public interface ISqlEventSourcedRepository<T>
        where T : class, IEventSourced
    {
        Task<Guid?> FindIdByUniqueIndexedProperty(string name, string value);
    }
}
