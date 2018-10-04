namespace Khala.EventSourcing.Sql
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public interface ISqlEventPublisher : IEventPublisher
    {
        Task FlushPendingEvents<T>(
            Guid sourceId,
            CancellationToken cancellationToken = default)
            where T : class, IEventSourced;
    }
}
