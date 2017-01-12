namespace ReactiveArchitecture.EventSourcing.Azure
{
    using System;
    using System.Threading.Tasks;

    public interface IAzureEventPublisher
    {
        Task PublishPendingEvents<T>(Guid sourceId)
            where T : class, IEventSourced;
    }
}
