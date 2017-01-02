namespace ReactiveArchitecture.EventSourcing
{
    using System;
    using System.Threading.Tasks;

    public interface IEventSourcedRepository<T>
        where T : class, IEventSourced
    {
        Task Save(T source);

        Task<T> Find(Guid sourceId);
    }
}
