namespace Khala.EventSourcing.Azure
{
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
    using static Khala.EventSourcing.Azure.PendingEventTableEntity;

    [TestClass]
    public class PendingEventTableEntity_specs
    {
        private IFixture _fixture;
        private IMessageSerializer _serializer;

        [TestInitialize]
        public void TestInitialize()
        {
            _fixture = new Fixture();
            _serializer = new JsonMessageSerializer();
        }

        [TestMethod]
        public void GetPartitionKey_has_guard_clauses()
        {
            var builder = new Fixture();
            new GuardClauseAssertion(builder).Verify(typeof(PendingEventTableEntity).GetMethod("GetPartitionKey"));
        }

        [TestMethod]
        public void PendingEventEntity_inherits_TableEntity()
        {
            typeof(PendingEventTableEntity).BaseType.Should().Be(typeof(TableEntity));
        }

        [TestMethod]
        public void FromEnvelope_has_guard_clauses()
        {
            _fixture.Customize(new AutoMoqCustomization());
            var assertion = new GuardClauseAssertion(_fixture);
            assertion.Verify(typeof(PendingEventTableEntity).GetMethod("FromEnvelope"));
        }

        [TestMethod]
        public void FromEnvelope_sets_PartitionKey_correctly()
        {
            var domainEvent = _fixture.Create<FakeUserCreated>();
            var envelope = new Envelope(domainEvent);

            PendingEventTableEntity actual =
                FromEnvelope<FakeUser>(envelope, _serializer);

            actual.PartitionKey.Should().Be(
                GetPartitionKey(typeof(FakeUser), domainEvent.SourceId));
        }

        [TestMethod]
        public void FromEnvelope_sets_RowKey_correctly()
        {
            var domainEvent = _fixture.Create<FakeUserCreated>();
            var envelope = new Envelope(domainEvent);

            PendingEventTableEntity actual =
                FromEnvelope<FakeUser>(envelope, _serializer);

            actual.RowKey.Should().Be(GetRowKey(domainEvent.Version));
        }

        [TestMethod]
        public void FromEnvelope_sets_PersistentPartition_correctly()
        {
            var domainEvent = _fixture.Create<FakeUserCreated>();
            var envelope = new Envelope(domainEvent);

            PendingEventTableEntity actual =
                FromEnvelope<FakeUser>(envelope, _serializer);

            actual.PersistentPartition.Should().Be(
                EventTableEntity.GetPartitionKey(
                    typeof(FakeUser), domainEvent.SourceId));
        }

        [TestMethod]
        public void FromEnvelope_sets_Version_correctly()
        {
            var domainEvent = _fixture.Create<FakeUserCreated>();
            var envelope = new Envelope(domainEvent);

            PendingEventTableEntity actual =
                FromEnvelope<FakeUser>(envelope, _serializer);

            actual.Version.Should().Be(domainEvent.Version);
        }

        [TestMethod]
        public void FromEnvelope_sets_MessageId_correctly()
        {
            var domainEvent = _fixture.Create<FakeUserCreated>();
            var envelope = new Envelope(domainEvent);

            PendingEventTableEntity actual =
                FromEnvelope<FakeUser>(envelope, _serializer);

            actual.MessageId.Should().Be(envelope.MessageId);
        }

        [TestMethod]
        public void FromEnvelope_sets_CorrelationId_correctly()
        {
            var domainEvent = _fixture.Create<FakeUserCreated>();
            var correlationId = GuidGenerator.Create();
            var envelope = new Envelope(GuidGenerator.Create(), domainEvent, correlationId: correlationId);

            PendingEventTableEntity actual =
                FromEnvelope<FakeUser>(envelope, _serializer);

            actual.CorrelationId.Should().Be(correlationId);
        }

        [TestMethod]
        public void FromEnvelope_sets_EventJson_correctly()
        {
            var domainEvent = _fixture.Create<FakeUserCreated>();
            var envelope = new Envelope(domainEvent);

            PendingEventTableEntity actual =
                FromEnvelope<FakeUser>(envelope, _serializer);

            object message = _serializer.Deserialize(actual.EventJson);
            message.Should().BeOfType<FakeUserCreated>();
            message.ShouldBeEquivalentTo(domainEvent);
        }

        [TestMethod]
        public void FromEnvelope_has_guard_clause_for_invalid_message()
        {
            var envelope = new Envelope(new object());
            Action action = () => FromEnvelope<FakeUser>(envelope, _serializer);
            action.ShouldThrow<ArgumentException>().Where(x => x.ParamName == "envelope");
        }
    }
}
