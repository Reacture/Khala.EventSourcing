using ReactiveArchitecture.EventSourcing;

namespace ReactiveArchitecture.FakeDomain.Events
{
    public class FakeUsernameChanged : DomainEvent
    {
        public string Username { get; set; }
    }
}
