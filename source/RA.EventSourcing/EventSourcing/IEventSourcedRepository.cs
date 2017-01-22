namespace ReactiveArchitecture.EventSourcing
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public interface IEventSourcedRepository<T>
        where T : class, IEventSourced
    {
        IEventPublisher EventPublisher { get; }

        Task Save(T source, CancellationToken cancellationToken);

        Task<T> Find(Guid sourceId, CancellationToken cancellationToken);
    }
}
