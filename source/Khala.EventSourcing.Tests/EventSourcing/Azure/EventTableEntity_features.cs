using System;
using FluentAssertions;
using Khala.FakeDomain;
using Khala.FakeDomain.Events;
using Khala.Messaging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.WindowsAzure.Storage.Table;
using Ploeh.AutoFixture;
using Ploeh.AutoFixture.AutoMoq;
using Ploeh.AutoFixture.Idioms;
using static Khala.EventSourcing.Azure.EventTableEntity;

namespace Khala.EventSourcing.Azure
{
    [TestClass]
    public class EventTableEntity_features
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
        public void EventTableEntity_inherits_TableEntity()
        {
            typeof(EventTableEntity).BaseType.Should().Be(typeof(TableEntity));
        }

        [TestMethod]
        public void FromEnvelope_has_guard_clauses()
        {
            fixture.Customize(new AutoMoqCustomization());
            var assertion = new GuardClauseAssertion(fixture);
            assertion.Verify(typeof(EventTableEntity).GetMethod("FromEnvelope"));
        }

        [TestMethod]
        public void FromEnvelope_has_guard_clause_for_invalid_message()
        {
            var envelope = new Envelope(new object());
            Action action = () => FromEnvelope<FakeUser>(envelope, serializer);
            action.ShouldThrow<ArgumentException>().Where(x => x.ParamName == "envelope");
        }

        [TestMethod]
        public void FromEnvelope_sets_PartitionKey_correctly()
        {
            var domainEvent = fixture.Create<FakeUserCreated>();
            var envelope = new Envelope(domainEvent);

            EventTableEntity entity =
                FromEnvelope<FakeUser>(envelope, serializer);

            entity.PartitionKey.Should().Be(
                GetPartitionKey(typeof(FakeUser), domainEvent.SourceId));
        }

        [TestMethod]
        public void FromEnvelope_sets_RowKey_correctly()
        {
            var domainEvent = fixture.Create<FakeUserCreated>();
            var envelope = new Envelope(domainEvent);

            EventTableEntity entity =
                FromEnvelope<FakeUser>(envelope, serializer);

            entity.RowKey.Should().Be(GetRowKey(domainEvent.Version));
        }

        [TestMethod]
        public void FromEnvelope_sets_Version_correctly()
        {
            var domainEvent = fixture.Create<FakeUserCreated>();
            var envelope = new Envelope(domainEvent);

            EventTableEntity entity =
                FromEnvelope<FakeUser>(envelope, serializer);

            entity.Version.Should().Be(domainEvent.Version);
        }

        [TestMethod]
        public void FromEnvelope_sets_EventType_correctly()
        {
            var domainEvent = fixture.Create<FakeUserCreated>();
            var envelope = new Envelope(domainEvent);

            EventTableEntity entity =
                FromEnvelope<FakeUser>(envelope, serializer);

            entity.EventType.Should().Be(typeof(FakeUserCreated).FullName);
        }

        [TestMethod]
        public void FromEnvelope_sets_MessageId_correctly()
        {
            var domainEvent = fixture.Create<FakeUserCreated>();
            var envelope = new Envelope(domainEvent);

            EventTableEntity entity =
                FromEnvelope<FakeUser>(envelope, serializer);

            entity.MessageId.Should().Be(envelope.MessageId);
        }

        [TestMethod]
        public void FromEnvelope_sets_CorrelationId_correctly()
        {
            var domainEvent = fixture.Create<FakeUserCreated>();
            var correlationId = Guid.NewGuid();
            var envelope = new Envelope(correlationId, domainEvent);

            EventTableEntity entity =
                FromEnvelope<FakeUser>(envelope, serializer);

            entity.CorrelationId.Should().Be(correlationId);
        }

        [TestMethod]
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

        [TestMethod]
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
