using ReactiveArchitecture.EventSourcing;

namespace ReactiveArchitecture.FakeDomain
{
    public class FakeUserMemento : IMemento
    {
        public int Version { get; set; }

        public string Username { get; set; }
    }
}
