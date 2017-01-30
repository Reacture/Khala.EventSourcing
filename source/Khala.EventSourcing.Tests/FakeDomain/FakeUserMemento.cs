using Khala.EventSourcing;

namespace Khala.FakeDomain
{
    public class FakeUserMemento : IMemento
    {
        public int Version { get; set; }

        public string Username { get; set; }
    }
}
