using System;
using System.Collections.Generic;
using ReactiveArchitecture.EventSourcing;
using ReactiveArchitecture.FakeDomain.Events;

namespace ReactiveArchitecture.FakeDomain
{
    public class FakeUser : EventSourced
    {
        public FakeUser(Guid id, string username)
            : base(id)
        {
            RaiseEvent(new FakeUserCreated { Username = username });
        }

        private FakeUser(Guid id, IEnumerable<IDomainEvent> pastEvents)
            : base(id)
        {
            HandlePastEvents(pastEvents);
        }

        public static FakeUser Factory(
            Guid id, IEnumerable<IDomainEvent> pastEvents)
        {
            return new FakeUser(id, pastEvents);
        }

        public void ChangeUsername(string username)
        {
            RaiseEvent(new FakeUsernameChanged { Username = username });
        }

        private void Handle(FakeUserCreated domainEvent)
        {
        }

        private void Handle(FakeUsernameChanged domainEvent)
        {
        }
    }
}
