namespace ReactiveArchitecture.EventSourcing.Azure
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using EventSourcing;

    public class AzureEventStore : IEventStore
    {
        public Task SaveEvents<T>(IEnumerable<IDomainEvent> events)
            where T : class, IEventSourced
        {
            if (events == null)
            {
                throw new ArgumentNullException(nameof(events));
            }

            throw new NotImplementedException();
        }

        public Task<IEnumerable<IDomainEvent>> LoadEvents<T>(
            Guid sourceId, int afterVersion = default(int))
            where T : class, IEventSourced
        {
            if (sourceId == Guid.Empty)
            {
                throw new ArgumentException(
                    $"{sourceId} cannot be empty", nameof(sourceId));
            }

            throw new NotImplementedException();
        }
    }
}
