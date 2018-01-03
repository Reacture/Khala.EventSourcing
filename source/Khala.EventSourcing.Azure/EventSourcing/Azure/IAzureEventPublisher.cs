namespace Khala.EventSourcing.Azure
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public interface IAzureEventPublisher : IEventPublisher
    {
        Task FlushPendingEvents<T>(
            Guid sourceId,
            CancellationToken cancellationToken = default)
            where T : class, IEventSourced;
    }
}
