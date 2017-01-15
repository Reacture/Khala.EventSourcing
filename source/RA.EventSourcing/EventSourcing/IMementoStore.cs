namespace ReactiveArchitecture.EventSourcing
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public interface IMementoStore
    {
        Task Save<T>(
            Guid sourceId,
            IMemento memento,
            CancellationToken cancellationToken = default(CancellationToken))
            where T : class, IEventSourced;

        Task<IMemento> Find<T>(
            Guid sourceId,
            CancellationToken cancellationToken = default(CancellationToken))
            where T : class, IEventSourced;

        Task Delete<T>(
            Guid sourceId,
            CancellationToken cancellationToken = default(CancellationToken))
            where T : class, IEventSourced;
    }
}
