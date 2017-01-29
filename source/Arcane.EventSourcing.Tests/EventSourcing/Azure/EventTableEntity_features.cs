using System;
using FluentAssertions;
using Microsoft.WindowsAzure.Storage.Table;
using Ploeh.AutoFixture;
using Ploeh.AutoFixture.AutoMoq;
using Ploeh.AutoFixture.Idioms;
using Arcane.FakeDomain;
using Arcane.FakeDomain.Events;
using Arcane.Messaging;
using Xunit;
using static Arcane.EventSourcing.Azure.EventTableEntity;

namespace Arcane.EventSourcing.Azure
{
    public class EventTableEntity_features
    {
        private IFixture fixture;
        private IMessageSerializer serializer;

        public EventTableEntity_features()
        {
            fixture = new Fixture();
            serializer = new JsonMessageSerializer();
        }

        [Fact]
        public void EventTableEntity_inherits_TableEntity()
        {
            typeof(EventTableEntity).BaseType.Should().Be(typeof(TableEntity));
        }

        [Fact]
        public void FromEnvelope_has_guard_clauses()
        {
            fixture.Customize(new AutoMoqCustomization());
            var assertion = new GuardClauseAssertion(fixture);
            assertion.Verify(typeof(EventTableEntity).GetMethod("FromEnvelope"));
        }

        [Fact]
        public void FromEnvelope_has_guard_clause_for_invalid_message()
        {
            var envelope = new Envelope(new object());
            Action action = () => FromEnvelope<FakeUser>(envelope, serializer);
            action.ShouldThrow<ArgumentException>().Where(x => x.ParamName == "envelope");
        }

        [Fact]
        public void FromEnvelope_sets_PartitionKey_correctly()
        {
            var domainEvent = fixture.Create<FakeUserCreated>();
            var envelope = new Envelope(domainEvent);

            EventTableEntity entity =
                FromEnvelope<FakeUser>(envelope, serializer);

            entity.PartitionKey.Should().Be(
                GetPartitionKey(typeof(FakeUser), domainEvent.SourceId));
        }

        [Fact]
        public void FromEnvelope_sets_RowKey_correctly()
        {
            var domainEvent = fixture.Create<FakeUserCreated>();
            var envelope = new Envelope(domainEvent);

            EventTableEntity entity =
                FromEnvelope<FakeUser>(envelope, serializer);

            entity.RowKey.Should().Be(GetRowKey(domainEvent.Version));
        }

        [Fact]
        public void FromEnvelope_sets_Version_correctly()
        {
            var domainEvent = fixture.Create<FakeUserCreated>();
            var envelope = new Envelope(domainEvent);

            EventTableEntity entity =
                FromEnvelope<FakeUser>(envelope, serializer);

            entity.Version.Should().Be(domainEvent.Version);
        }

        [Fact]
        public void FromEnvelope_sets_EventType_correctly()
        {
            var domainEvent = fixture.Create<FakeUserCreated>();
            var envelope = new Envelope(domainEvent);

            EventTableEntity entity =
                FromEnvelope<FakeUser>(envelope, serializer);

            entity.EventType.Should().Be(typeof(FakeUserCreated).FullName);
        }

        [Fact]
        public void FromEnvelope_sets_MessageId_correctly()
        {
            var domainEvent = fixture.Create<FakeUserCreated>();
            var envelope = new Envelope(domainEvent);

            EventTableEntity entity =
                FromEnvelope<FakeUser>(envelope, serializer);

            entity.MessageId.Should().Be(envelope.MessageId);
        }

        [Fact]
        public void FromEnvelope_sets_CorrelationId_correctly()
        {
            var domainEvent = fixture.Create<FakeUserCreated>();
            var correlationId = Guid.NewGuid();
            var envelope = new Envelope(correlationId, domainEvent);

            EventTableEntity entity =
                FromEnvelope<FakeUser>(envelope, serializer);

            entity.CorrelationId.Should().Be(correlationId);
        }

        [Fact]
        public void FromEnvelope_sets_EventJson_correctly()
        {
            var domainEvent = fixture.Create<FakeUserCreated>();
            var envelope = new Envelope(domainEvent);

            EventTableEntity entity =
                FromEnvelope<FakeUser>(envelope, serializer);

            object actual = serializer.Deserialize(entity.EventJson);
            actual.Should().BeOfType<FakeUserCreated>();
            actual.ShouldBeEquivalentTo(domainEvent);
        }

        [Fact]
        public void FromEnvelope_sets_RaisedAt_correctly()
        {
            var domainEvent = fixture.Create<FakeUserCreated>();
            var envelope = new Envelope(domainEvent);

            EventTableEntity entity =
                FromEnvelope<FakeUser>(envelope, serializer);

            entity.RaisedAt.Should().Be(domainEvent.RaisedAt);
        }
    }
}
