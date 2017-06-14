namespace Khala.FakeDomain
{
    using Khala.EventSourcing;

    public class FakeUserMemento : IMemento
    {
        public int Version { get; set; }

        public string Username { get; set; }
    }
}
