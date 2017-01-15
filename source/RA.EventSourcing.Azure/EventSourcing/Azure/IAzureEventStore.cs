namespace ReactiveArchitecture.EventSourcing.Azure
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    public interface IAzureEventStore
    {
        Task SaveEvents<T>(
            IEnumerable<IDomainEvent> events,
            CancellationToken cancellationToken)
            where T : class, IEventSourced;

        Task<IEnumerable<IDomainEvent>> LoadEvents<T>(
            Guid sourceId,
            int afterVersion,
            CancellationToken cancellationToken)
            where T : class, IEventSourced;
    }
}
