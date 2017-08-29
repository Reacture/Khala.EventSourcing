namespace Khala.EventSourcing.Azure
{
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
        private IFixture fixture;
        private IMessageSerializer serializer;

        [TestInitialize]
        public void TestInitialize()
        {
            fixture = new Fixture();
            serializer = new JsonMessageSerializer();
        }

        [TestMethod]
        public void PendingEventEntity_inherits_TableEntity()
        {
            typeof(PendingEventTableEntity).BaseType.Should().Be(typeof(TableEntity));
        }

        [TestMethod]
        public void FromEnvelope_has_guard_clauses()
        {
            fixture.Customize(new AutoMoqCustomization());
            var assertion = new GuardClauseAssertion(fixture);
            assertion.Verify(typeof(PendingEventTableEntity).GetMethod("FromEnvelope"));
        }

        [TestMethod]
        public void FromEnvelope_sets_PartitionKey_correctly()
        {
            var domainEvent = fixture.Create<FakeUserCreated>();
            var envelope = new Envelope(domainEvent);

            PendingEventTableEntity actual =
                FromEnvelope<FakeUser>(envelope, serializer);

            actual.PartitionKey.Should().Be(
                GetPartitionKey(typeof(FakeUser), domainEvent.SourceId));
        }

        [TestMethod]
        public void FromEnvelope_sets_RowKey_correctly()
        {
            var domainEvent = fixture.Create<FakeUserCreated>();
            var envelope = new Envelope(domainEvent);

            PendingEventTableEntity actual =
                FromEnvelope<FakeUser>(envelope, serializer);

            actual.RowKey.Should().Be(GetRowKey(domainEvent.Version));
        }

        [TestMethod]
        public void FromEnvelope_sets_PersistentPartition_correctly()
        {
            var domainEvent = fixture.Create<FakeUserCreated>();
            var envelope = new Envelope(domainEvent);

            PendingEventTableEntity actual =
                FromEnvelope<FakeUser>(envelope, serializer);

            actual.PersistentPartition.Should().Be(
                EventTableEntity.GetPartitionKey(
                    typeof(FakeUser), domainEvent.SourceId));
        }

        [TestMethod]
        public void FromEnvelope_sets_Version_correctly()
        {
            var domainEvent = fixture.Create<FakeUserCreated>();
            var envelope = new Envelope(domainEvent);

            PendingEventTableEntity actual =
                FromEnvelope<FakeUser>(envelope, serializer);

            actual.Version.Should().Be(domainEvent.Version);
        }

        [TestMethod]
        public void FromEnvelope_sets_MessageId_correctly()
        {
            var domainEvent = fixture.Create<FakeUserCreated>();
            var envelope = new Envelope(domainEvent);

            PendingEventTableEntity actual =
                FromEnvelope<FakeUser>(envelope, serializer);

            actual.MessageId.Should().Be(envelope.MessageId);
        }

        [TestMethod]
        public void FromEnvelope_sets_CorrelationId_correctly()
        {
            var domainEvent = fixture.Create<FakeUserCreated>();
            var correlationId = GuidGenerator.Create();
            var envelope = new Envelope(correlationId, domainEvent);

            PendingEventTableEntity actual =
                FromEnvelope<FakeUser>(envelope, serializer);

            actual.CorrelationId.Should().Be(correlationId);
        }

        [TestMethod]
        public void FromEnvelope_sets_EventJson_correctly()
        {
            var domainEvent = fixture.Create<FakeUserCreated>();
            var envelope = new Envelope(domainEvent);

            PendingEventTableEntity actual =
                FromEnvelope<FakeUser>(envelope, serializer);

            object message = serializer.Deserialize(actual.EventJson);
            message.Should().BeOfType<FakeUserCreated>();
            message.ShouldBeEquivalentTo(domainEvent);
        }
    }
}
