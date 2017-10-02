namespace Khala.EventSourcing.Sql
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public interface ISqlEventSourcedRepository<T> : IEventSourcedRepository<T>
        where T : class, IEventSourced
    {
        Task<Guid?> FindIdByUniqueIndexedProperty(
            string name, string value, CancellationToken cancellationToken);
    }
}
