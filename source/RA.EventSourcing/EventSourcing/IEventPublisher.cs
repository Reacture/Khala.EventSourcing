namespace ReactiveArchitecture.EventSourcing
{
    using System;
    using System.Threading.Tasks;

    public interface IEventPublisher
    {
        Task PublishPendingEvents<T>(Guid sourceId)
            where T : class, IEventSourced;

        void EnqueueAll();
    }
}
