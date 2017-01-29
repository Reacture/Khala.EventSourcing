using System;
using System.Collections.Generic;
using Arcane.EventSourcing;
using Arcane.FakeDomain.Events;

namespace Arcane.FakeDomain
{
    public class FakeUser : EventSourced, IMementoOriginator
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

        private FakeUser(
            Guid id,
            FakeUserMemento memento,
            IEnumerable<IDomainEvent> pastEvents)
            : base(id, memento)
        {
            Username = memento.Username;
            HandlePastEvents(pastEvents);
        }

        public string Username { get; private set; }

        public static FakeUser Factory(
            Guid id, IEnumerable<IDomainEvent> pastEvents)
        {
            return new FakeUser(id, pastEvents);
        }

        public static FakeUser Factory(
            Guid id, IMemento memento, IEnumerable<IDomainEvent> pastEvents)
        {
            return new FakeUser(id, (FakeUserMemento)memento, pastEvents);
        }

        public void ChangeUsername(string username)
        {
            RaiseEvent(new FakeUsernameChanged { Username = username });
        }

        public IMemento SaveToMemento()
        {
            return new FakeUserMemento
            {
                Version = Version,
                Username = Username
            };
        }

        private void Handle(FakeUserCreated domainEvent)
        {
            Username = domainEvent.Username;
        }

        private void Handle(FakeUsernameChanged domainEvent)
        {
            Username = domainEvent.Username;
        }
    }
}
