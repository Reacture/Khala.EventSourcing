namespace ReactiveArchitecture.EventSourcing.Sql
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public interface ISqlEventPublisher : IEventPublisher
    {
        Task PublishPendingEvents<T>(
            Guid sourceId,
            CancellationToken cancellationToken)
            where T : class, IEventSourced;
    }
}
