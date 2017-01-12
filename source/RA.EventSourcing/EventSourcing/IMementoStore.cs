namespace ReactiveArchitecture.EventSourcing
{
    using System;
    using System.Threading.Tasks;

    public interface IMementoStore
    {
        Task Save<T>(IMemento memento);

        IMemento Find<T>(Guid sourceId);
    }
}
