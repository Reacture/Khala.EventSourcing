namespace ReactiveArchitecture.EventSourcing.Azure
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public interface IAzureEventPublisher : IEventPublisher
    {
        Task PublishPendingEvents<T>(
            Guid sourceId,
            CancellationToken cancellationToken)
            where T : class, IEventSourced;
    }
}
