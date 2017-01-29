using Arcane.EventSourcing;

namespace Arcane.FakeDomain
{
    public class FakeUserMemento : IMemento
    {
        public int Version { get; set; }

        public string Username { get; set; }
    }
}
