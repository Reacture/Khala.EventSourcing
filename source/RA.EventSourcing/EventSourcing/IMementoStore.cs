namespace ReactiveArchitecture.EventSourcing
{
    using System;
    using System.Threading.Tasks;

    public interface IMementoStore
    {
        Task Save<T>(Guid sourceId, IMemento memento)
            where T : class, IEventSourced;

        Task<IMemento> Find<T>(Guid sourceId)
            where T : class, IEventSourced;

        Task Delete<T>(Guid sourceId)
            where T : class, IEventSourced;
    }
}
