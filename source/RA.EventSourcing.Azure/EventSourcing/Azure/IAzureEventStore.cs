namespace ReactiveArchitecture.EventSourcing.Azure
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public interface IAzureEventStore
    {
        Task SaveEvents<T>(IEnumerable<IDomainEvent> events)
            where T : class, IEventSourced;

        Task<IEnumerable<IDomainEvent>> LoadEvents<T>(
            Guid sourceId, int afterVersion = default(int))
            where T : class, IEventSourced;
    }
}
