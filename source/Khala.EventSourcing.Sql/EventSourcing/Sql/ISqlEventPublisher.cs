namespace Khala.EventSourcing.Sql
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public interface ISqlEventPublisher : IEventPublisher
    {
        Task PublishPendingEvents(
            Guid sourceId,
            CancellationToken cancellationToken);
    }
}
