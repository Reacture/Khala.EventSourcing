using System;
using FluentAssertions;
using Khala.FakeDomain.Events;
using Khala.Messaging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Ploeh.AutoFixture;

namespace Khala.EventSourcing.Sql
{
    [TestClass]
    public class PendingEvent_features
    {
        private IFixture fixture;
        private IMessageSerializer serializer;

        [TestInitialize]
        public void TestInitialize()
        {
            fixture = new Fixture();
            serializer = new JsonMessageSerializer();
        }

        [TestMethod]
        public void FromEnvelope_sets_AggregateId_correctly()
        {
            var domainEvent = fixture.Create<FakeUserCreated>();
            var envelope = new Envelope(domainEvent);
            var actual = PendingEvent.FromEnvelope(envelope, serializer);
            actual.AggregateId.Should().Be(domainEvent.SourceId);
        }

        [TestMethod]
        public void FromEnvelope_sets_Version_correctly()
        {
            var domainEvent = fixture.Create<FakeUserCreated>();
            var envelope = new Envelope(domainEvent);
            var actual = PendingEvent.FromEnvelope(envelope, serializer);
            actual.Version.Should().Be(domainEvent.Version);
        }

        [TestMethod]
        public void FromEnvelope_sets_MessageId_correctly()
        {
            var domainEvent = fixture.Create<FakeUserCreated>();
            var envelope = new Envelope(domainEvent);
            var actual = PendingEvent.FromEnvelope(envelope, serializer);
            actual.MessageId.Should().Be(envelope.MessageId);
        }

        [TestMethod]
        public void FromEnvelope_sets_CorrelationId_correctly()
        {
            var domainEvent = fixture.Create<FakeUserCreated>();
            var correlationId = Guid.NewGuid();
            var envelope = new Envelope(correlationId, domainEvent);
            var actual = PendingEvent.FromEnvelope(envelope, serializer);
            actual.CorrelationId.Should().Be(correlationId);
        }

        [TestMethod]
        public void FromEnvelope_sets_EventJson_correctly()
        {
            var domainEvent = fixture.Create<FakeUserCreated>();
            var envelope = new Envelope(domainEvent);

            var actual = PendingEvent.FromEnvelope(envelope, serializer);

            object message = serializer.Deserialize(actual.EventJson);
            message.Should().BeOfType<FakeUserCreated>();
            message.ShouldBeEquivalentTo(domainEvent);
        }

        [TestMethod]
        public void FromEnvelope_has_guard_clause_for_invalid_message()
        {
            var envelope = new Envelope(new object());
            Action action = () => PendingEvent.FromEnvelope(envelope, serializer);
            action.ShouldThrow<ArgumentException>().Where(x => x.ParamName == "envelope");
        }
    }
}
