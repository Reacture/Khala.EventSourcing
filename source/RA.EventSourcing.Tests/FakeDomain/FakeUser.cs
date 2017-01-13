using System;
using System.Collections.Generic;
using ReactiveArchitecture.EventSourcing;
using ReactiveArchitecture.FakeDomain.Events;

namespace ReactiveArchitecture.FakeDomain
{
    public class FakeUser : EventSourced
    {
        public FakeUser(Guid id, string username)
            : this(id)
        {
            RaiseEvent(new FakeUserCreated { Username = username });
        }

        private FakeUser(Guid id)
            : base(id)
        {
            SetEventHandler<FakeUserCreated>(Handle);
            SetEventHandler<FakeUsernameChanged>(Handle);
        }

        public static FakeUser Factory(
            Guid id, IEnumerable<IDomainEvent> pastEvents)
        {
            var user = new FakeUser(id);
            user.HandlePastEvents(pastEvents);
            return user;
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
