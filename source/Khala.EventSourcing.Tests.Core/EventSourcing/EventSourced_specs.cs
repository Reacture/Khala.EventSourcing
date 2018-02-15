namespace Khala.EventSourcing
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using AutoFixture;
    using AutoFixture.AutoMoq;
    using AutoFixture.Idioms;
    using FluentAssertions;
    using Khala.FakeDomain;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class EventSourced_specs
    {
        private abstract class AbstractAggregate : EventSourced
        {
            protected AbstractAggregate(Guid id)
                : base(id)
            {
            }

            public Guid Property { get; private set; }

            private void Handle(SomeDomainEvent domainEvent)
            {
                Property = domainEvent.Property;
            }
        }

        private class ConcreteAggregate : AbstractAggregate
        {
            public ConcreteAggregate(Guid id)
                : base(id)
            {
            }

            public void RaiseSomeDomainEvent(Guid value)
            {
                RaiseEvent(new SomeDomainEvent { Property = value });
            }
        }

        private class SomeDomainEvent : DomainEvent
        {
            public Guid Property { get; set; }
        }

        private class EventSourcedProxy : EventSourced
        {
            public EventSourcedProxy(Guid id)
                : base(id)
            {
            }

            public EventSourcedProxy(Guid id, IMemento memento)
                : base(id, memento)
            {
            }

            public void SetEventHandler<TEvent>(Action<TEvent> handler)
                where TEvent : IDomainEvent
            {
                SetEventHandler(
                    handler == null
                    ? default(DomainEventHandler<TEvent>)
                    : e => handler.Invoke(e));
            }

            public new void HandlePastEvents(IEnumerable<IDomainEvent> pastEvents)
            {
                base.HandlePastEvents(pastEvents);
            }

            [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1061:DoNotHideBaseClassMethods", Justification = "Proxy for testing.")]
            public new void RaiseEvent<TEvent>(TEvent domainEvent)
                where TEvent : IDomainEvent
            {
                base.RaiseEvent(domainEvent);
            }
        }

        [TestMethod]
        public void sut_binds_domain_event_handlers_of_base_class_correctly()
        {
            // Arrange
            var sut = new ConcreteAggregate(Guid.NewGuid());
            var expected = Guid.NewGuid();

            // Act
            Action action = () => sut.RaiseSomeDomainEvent(expected);

            // Assert
            action.ShouldNotThrow();
            sut.Property.Should().Be(expected);
        }

        [TestMethod]
        public void constructors_has_null_guard_clauses()
        {
            IFixture fixture = new Fixture().Customize(new AutoMoqCustomization());
            var assertion = new GuardClauseAssertion(fixture);
            assertion.Verify(typeof(EventSourcedProxy).GetConstructors());
        }

        [TestMethod]
        public void SetEventHandler_has_null_guard_clause()
        {
            var fixture = new Fixture();
            var assertion = new GuardClauseAssertion(fixture);
            assertion.Verify(typeof(EventSourcedProxy).GetMethod("SetEventHandler"));
        }

        [TestMethod]
        public void SetEventHandler_has_guard_clause_against_duplicate_event_type()
        {
            var sut = new EventSourcedProxy(Guid.NewGuid());
            sut.SetEventHandler<SomeDomainEvent>(e => { });

            Action action = () => sut.SetEventHandler<SomeDomainEvent>(e => { });

            action.ShouldThrow<InvalidOperationException>();
        }

        [TestMethod]
        public void HandlePastEvents_has_null_guard_clause()
        {
            IFixture fixture = new Fixture().Customize(new AutoMoqCustomization());
            var assertion = new GuardClauseAssertion(fixture);
            assertion.Verify(typeof(EventSourcedProxy).GetMethod("HandlePastEvents"));
        }

        [TestMethod]
        public void HandlePastEvents_has_guard_clause_against_null_event_element()
        {
            var sut = new EventSourcedProxy(Guid.NewGuid());
            sut.SetEventHandler<SomeDomainEvent>(e => { });
            IDomainEvent[] pastEvents = new IDomainEvent[]
            {
                new SomeDomainEvent
                {
                    SourceId = sut.Id,
                    Version = 1,
                    RaisedAt = DateTime.UtcNow,
                    Property = Guid.NewGuid(),
                },
                null,
            };

            Action action = () => sut.HandlePastEvents(pastEvents);

            action.ShouldThrow<ArgumentException>();
        }

        [TestMethod]
        public void HandlePastEvents_has_guard_clause_against_invalid_SourceId()
        {
            var sut = new EventSourcedProxy(Guid.NewGuid());
            sut.SetEventHandler<SomeDomainEvent>(e => { });
            IDomainEvent[] pastEvents = new IDomainEvent[]
            {
                new SomeDomainEvent
                {
                    SourceId = Guid.NewGuid(),
                    Version = 1,
                    RaisedAt = DateTime.UtcNow,
                    Property = Guid.NewGuid(),
                },
            };

            Action action = () => sut.HandlePastEvents(pastEvents);

            action.ShouldThrow<ArgumentException>();
        }

        [TestMethod]
        public void HandlePastEvents_has_guard_clause_against_invalid_Version()
        {
            var sut = new EventSourcedProxy(Guid.NewGuid());
            sut.SetEventHandler<SomeDomainEvent>(e => { });
            IDomainEvent[] pastEvents = new IDomainEvent[]
            {
                new SomeDomainEvent
                {
                    SourceId = sut.Id,
                    Version = 1,
                    RaisedAt = DateTime.UtcNow,
                    Property = Guid.NewGuid(),
                },
                new SomeDomainEvent
                {
                    SourceId = sut.Id,
                    Version = 1,
                    RaisedAt = DateTime.UtcNow,
                    Property = Guid.NewGuid(),
                },
            };

            Action action = () => sut.HandlePastEvents(pastEvents);

            action.ShouldThrow<ArgumentException>();
        }

        [TestMethod]
        public void HandlePastEvents_fails_for_unknown_domain_event_type()
        {
            var sut = new EventSourcedProxy(Guid.NewGuid());
            IDomainEvent[] pastEvents = new IDomainEvent[]
            {
                new SomeDomainEvent
                {
                    SourceId = sut.Id,
                    Version = 1,
                    RaisedAt = DateTime.UtcNow,
                    Property = Guid.NewGuid(),
                },
            };

            Action action = () => sut.HandlePastEvents(pastEvents);

            action.ShouldThrow<InvalidOperationException>();
        }

        [TestMethod]
        public void FlushPendingEvents_returns_all_pending_events()
        {
            var fixture = new Fixture();
            var sut = new FakeUser(Guid.NewGuid(), fixture.Create<string>());
            var expected = sut.PendingEvents.ToList();

            IEnumerable<IDomainEvent> actual = sut.FlushPendingEvents();

            actual.Should().Equal(expected);
        }

        [TestMethod]
        public void FlushPendingEvents_clears_pending_events()
        {
            var fixture = new Fixture();
            var sut = new FakeUser(Guid.NewGuid(), fixture.Create<string>());

            sut.FlushPendingEvents();

            sut.PendingEvents.Should().BeEmpty();
        }

        [TestMethod]
        public void RaiseEvent_has_null_guard_clause()
        {
            var sut = new EventSourcedProxy(Guid.NewGuid());
            Action action = () => sut.RaiseEvent<SomeDomainEvent>(null);
            action.ShouldThrow<ArgumentNullException>().Which.ParamName.Should().Be("domainEvent");
        }
    }
}
