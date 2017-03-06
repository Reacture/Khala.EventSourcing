using System;
using FluentAssertions;
using Khala.FakeDomain.Events;
using Khala.Messaging;
using Ploeh.AutoFixture;
using Xunit;

namespace Khala.EventSourcing.Sql
{
    public class PendingEvent_features
    {
        private IFixture fixture;
        private IMessageSerializer serializer;

        public PendingEvent_features()
        {
            fixture = new Fixture();
            serializer = new JsonMessageSerializer();
        }

        [Fact]
        public void FromEnvelope_sets_AggregateId_correctly()
        {
            var domainEvent = fixture.Create<FakeUserCreated>();
            var envelope = new Envelope(domainEvent);
            var actual = PendingEvent.FromEnvelope(envelope, Guid.NewGuid(), serializer);
            actual.AggregateId.Should().Be(domainEvent.SourceId);
        }

        [Fact]
        public void FromEnvelope_sets_Version_correctly()
        {
            var domainEvent = fixture.Create<FakeUserCreated>();
            var envelope = new Envelope(domainEvent);
            var actual = PendingEvent.FromEnvelope(envelope, Guid.NewGuid(), serializer);
            actual.Version.Should().Be(domainEvent.Version);
        }

        [Fact]
        public void FromEnvelope_sets_MessageId_correctly()
        {
            var domainEvent = fixture.Create<FakeUserCreated>();
            var envelope = new Envelope(domainEvent);
            var actual = PendingEvent.FromEnvelope(envelope, Guid.NewGuid(), serializer);
            actual.MessageId.Should().Be(envelope.MessageId);
        }

        [Fact]
        public void FromEnvelope_sets_CorrelationId_correctly()
        {
            var domainEvent = fixture.Create<FakeUserCreated>();
            var correlationId = Guid.NewGuid();
            var envelope = new Envelope(correlationId, domainEvent);
            var actual = PendingEvent.FromEnvelope(envelope, Guid.NewGuid(), serializer);
            actual.CorrelationId.Should().Be(correlationId);
        }

        [Fact]
        public void FromEnvelope_sets_BatchGroup_correctly()
        {
            var domainEvent = fixture.Create<FakeUserCreated>();
            var envelope = new Envelope(domainEvent);
            var batchGroup = Guid.NewGuid();
            var actual = PendingEvent.FromEnvelope(envelope, batchGroup, serializer);
            actual.BatchGroup.Should().Be(batchGroup);
        }

        [Fact]
        public void FromEnvelope_sets_EventJson_correctly()
        {
            var domainEvent = fixture.Create<FakeUserCreated>();
            var envelope = new Envelope(domainEvent);

            var actual = PendingEvent.FromEnvelope(envelope, Guid.NewGuid(), serializer);

            object message = serializer.Deserialize(actual.EventJson);
            message.Should().BeOfType<FakeUserCreated>();
            message.ShouldBeEquivalentTo(domainEvent);
        }

        [Fact]
        public void FromEnvelope_has_guard_clause_for_invalid_message()
        {
            var envelope = new Envelope(new object());
            Action action = () => PendingEvent.FromEnvelope(envelope, Guid.NewGuid(), serializer);
            action.ShouldThrow<ArgumentException>().Where(x => x.ParamName == "envelope");
        }
    }
}
