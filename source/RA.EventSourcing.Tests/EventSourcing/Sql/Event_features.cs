using System;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Ploeh.AutoFixture;
using Ploeh.AutoFixture.AutoMoq;
using Ploeh.AutoFixture.Idioms;
using ReactiveArchitecture.Messaging;

namespace ReactiveArchitecture.EventSourcing.Sql
{
    [TestClass]
    public class Event_features
    {
        public class FakeDomainEvent : DomainEvent
        {
            public Guid GuidProp { get; set; }

            public int Int32Prop { get; set; }

            public double DoubleProp { get; set; }

            public string StringProp { get; set; }
        }

        private IFixture fixture =
            new Fixture().Customize(new AutoMoqCustomization());

        [TestMethod]
        public void FromDomainEvent_has_guard_clauses()
        {
            var assertion = new GuardClauseAssertion(fixture);
            assertion.Verify(typeof(Event).GetMethod("FromDomainEvent"));
        }

        [TestMethod]
        public void FromDomainEvent_sets_AggregatId_correctly()
        {
            var domainEvent = fixture.Create<FakeDomainEvent>();
            var actual = Event.FromDomainEvent(
                domainEvent, new JsonMessageSerializer());
            actual.AggregateId.Should().Be(domainEvent.SourceId);
        }

        [TestMethod]
        public void FromDomainEvent_sets_Version_correctly()
        {
            var domainEvent = fixture.Create<FakeDomainEvent>();
            var actual = Event.FromDomainEvent(
                domainEvent, new JsonMessageSerializer());
            actual.Version.Should().Be(domainEvent.Version);
        }

        [TestMethod]
        public void FromDomainEvent_sets_EventType_correctly()
        {
            var domainEvent = fixture.Create<FakeDomainEvent>();
            var actual = Event.FromDomainEvent(
                domainEvent, new JsonMessageSerializer());
            actual.EventType.Should().Be(typeof(FakeDomainEvent).FullName);
        }

        [TestMethod]
        public void FromDomainEvent_sets_PayloadJson_correctly()
        {
            var serializer = new JsonMessageSerializer();
            var domainEvent = fixture.Create<FakeDomainEvent>();

            var actual = Event.FromDomainEvent(domainEvent, serializer);

            object deserialized = serializer.Deserialize(actual.PayloadJson);
            deserialized.Should().BeOfType<FakeDomainEvent>();
            deserialized.ShouldBeEquivalentTo(domainEvent);
        }

        [TestMethod]
        public void FromDomainEvent_sets_RaisedAt_correctly()
        {
            var domainEvent = fixture.Create<FakeDomainEvent>();
            var actual = Event.FromDomainEvent(
                domainEvent, new JsonMessageSerializer());
            actual.RaisedAt.Should().Be(domainEvent.RaisedAt);
        }
    }
}
