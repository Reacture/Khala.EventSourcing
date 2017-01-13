namespace ReactiveArchitecture.EventSourcing
{
    using System;
    using System.Threading.Tasks;

    public interface IMementoStore
    {
        Task Save<T>(IMemento memento)
            where T : class, IEventSourced;

        Task<IMemento> Find<T>(Guid sourceId)
            where T : class, IEventSourced;
    }
}
