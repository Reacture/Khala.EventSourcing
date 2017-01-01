using ReactiveArchitecture.EventSourcing;

namespace ReactiveArchitecture.FakeDomain.Events
{
    public class FakeUserCreated : DomainEvent
    {
        public string Username { get; set; }
    }
}
