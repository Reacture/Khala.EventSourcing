namespace Khala.EventSourcing.Azure
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    public interface IAzureEventStore
    {
        Task SaveEvents<T>(
            IEnumerable<IDomainEvent> events,
            Guid? operationId = default,
            Guid? correlationId = default,
            string contributor = default,
            CancellationToken cancellationToken = default)
            where T : class, IEventSourced;

        Task<IEnumerable<IDomainEvent>> LoadEvents<T>(
            Guid sourceId,
            int afterVersion = default,
            CancellationToken cancellationToken = default)
            where T : class, IEventSourced;
    }
}
