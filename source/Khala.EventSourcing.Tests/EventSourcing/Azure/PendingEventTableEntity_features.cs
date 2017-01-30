using FluentAssertions;
using Khala.FakeDomain;
using Khala.FakeDomain.Events;
using Khala.Messaging;
using Microsoft.WindowsAzure.Storage.Table;
using Ploeh.AutoFixture;
using Ploeh.AutoFixture.AutoMoq;
using Ploeh.AutoFixture.Idioms;
using Xunit;
using static Khala.EventSourcing.Azure.PendingEventTableEntity;

namespace Khala.EventSourcing.Azure
{
    public class PendingEventTableEntity_features
    {
        private IFixture fixture;
        private IMessageSerializer serializer;

        public PendingEventTableEntity_features()
        {
            fixture = new Fixture();
            serializer = new JsonMessageSerializer();
        }

        [Fact]
        public void PendingEventEntity_inherits_TableEntity()
        {
            typeof(PendingEventTableEntity).BaseType.Should().Be(typeof(TableEntity));
        }

        [Fact]
        public void FromEnvelope_has_guard_clauses()
        {
            fixture.Customize(new AutoMoqCustomization());
            var assertion = new GuardClauseAssertion(fixture);
            assertion.Verify(typeof(PendingEventTableEntity).GetMethod("FromEnvelope"));
        }

        [Fact]
        public void FromEnvelope_sets_PartitionKey_correctly()
        {
            var domainEvent = fixture.Create<FakeUserCreated>();
            var envelope = new Envelope(domainEvent);

            PendingEventTableEntity actual =
                FromEnvelope<FakeUser>(envelope, serializer);

            actual.PartitionKey.Should().Be(
                GetPartitionKey(typeof(FakeUser), domainEvent.SourceId));
        }

        [Fact]
        public void FromEnvelope_sets_RowKey_correctly()
        {
            var domainEvent = fixture.Create<FakeUserCreated>();
            var envelope = new Envelope(domainEvent);

            PendingEventTableEntity actual =
                FromEnvelope<FakeUser>(envelope, serializer);

            actual.RowKey.Should().Be(GetRowKey(domainEvent.Version));
        }

        [Fact]
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

        [Fact]
        public void FromEnvelope_sets_Version_correctly()
        {
            var domainEvent = fixture.Create<FakeUserCreated>();
            var envelope = new Envelope(domainEvent);

            PendingEventTableEntity actual =
                FromEnvelope<FakeUser>(envelope, serializer);

            actual.Version.Should().Be(domainEvent.Version);
        }

        [Fact]
        public void FromEnvelope_sets_EnvelopeJson_correctly()
        {
            var domainEvent = fixture.Create<FakeUserCreated>();
            var envelope = new Envelope(domainEvent);

            PendingEventTableEntity actual =
                FromEnvelope<FakeUser>(envelope, serializer);

            object restored = serializer.Deserialize(actual.EnvelopeJson);
            restored.Should().BeOfType<Envelope>();
            restored.ShouldBeEquivalentTo(envelope);
        }
    }
}
