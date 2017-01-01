using System;
using System.Collections.Generic;
using ReactiveArchitecture.EventSourcing;

namespace ReactiveArchitecture.FakeDomain
{
    public class FakeUser : IEventSourced
    {
        public Guid Id { get; set; }

        public int Version { get; set; }

        public IEnumerable<IDomainEvent> PendingEvents { get; set; }
    }
}
