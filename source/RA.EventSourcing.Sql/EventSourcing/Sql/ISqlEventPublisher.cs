namespace ReactiveArchitecture.EventSourcing.Sql
{
    using System;
    using System.Threading.Tasks;

    public interface ISqlEventPublisher
    {
        Task PublishPendingEvents<T>(Guid sourceId)
            where T : class, IEventSourced;

        void EnqueueAll();
    }
}
