namespace ReactiveArchitecture.EventSourcing
{
    using System.Collections.Generic;

    public interface IEventSourced
    {
        IEnumerable<IDomainEvent> PendingEvents { get; }
    }
}
