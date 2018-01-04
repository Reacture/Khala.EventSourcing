namespace Khala.EventSourcing.Sql
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public interface ISqlEventPublisher : IEventPublisher
    {
        Task FlushPendingEvents(
            Guid sourceId,
            CancellationToken cancellationToken = default);
    }
}
