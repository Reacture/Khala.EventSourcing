using System;
using ReactiveArchitecture.EventSourcing;

namespace ReactiveArchitecture.FakeDomain
{
    public class FakeUserMemento : IMemento
    {
        public Guid SourceId { get; set; }

        public int Version { get; set; }

        public string Username { get; set; }
    }
}
