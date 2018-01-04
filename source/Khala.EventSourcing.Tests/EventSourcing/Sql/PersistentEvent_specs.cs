namespace Khala.EventSourcing.Sql
{
    using System;
    using FluentAssertions;
    using Khala.Messaging;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Ploeh.AutoFixture;
    using Ploeh.AutoFixture.AutoMoq;
    using Ploeh.AutoFixture.Idioms;

    [TestClass]
    public class PersistentEvent_specs
    {
        private class FakeDomainEvent : DomainEvent
        {
            public Guid GuidProp { get; set; }

            public int Int32Prop { get; set; }

            public double DoubleProp { get; set; }

            public string StringProp { get; set; }
        }

        private IFixture _fixture =
            new Fixture().Customize(new AutoMoqCustomization());

        [TestMethod]
        public void FromEnvelope_has_guard_clauses()
        {
            var assertion = new GuardClauseAssertion(_fixture);
            assertion.Verify(typeof(PersistentEvent).GetMethod("FromEnvelope"));
        }

        [TestMethod]
        public void FromEnvelope_has_guard_clause_for_invalid_message()
        {
            var envelope = new Envelope(new object());
            Action action = () => PersistentEvent.FromEnvelope(envelope, new JsonMessageSerializer());
            action.ShouldThrow<ArgumentException>().Where(x => x.ParamName == "envelope");
        }

        [TestMethod]
        public void FromEnvelope_sets_AggregateId_correctly()
        {
            FakeDomainEvent domainEvent = _fixture.Create<FakeDomainEvent>();
            var envelope = new Envelope(domainEvent);
            var actual = PersistentEvent.FromEnvelope(
                envelope, new JsonMessageSerializer());
            actual.AggregateId.Should().Be(domainEvent.SourceId);
        }

        [TestMethod]
        public void FromEnvelope_sets_Version_correctly()
        {
            FakeDomainEvent domainEvent = _fixture.Create<FakeDomainEvent>();
            var envelope = new Envelope(domainEvent);
            var actual = PersistentEvent.FromEnvelope(
                envelope, new JsonMessageSerializer());
            actual.Version.Should().Be(domainEvent.Version);
        }

        [TestMethod]
        public void FromEnvelope_sets_EventType_correctly()
        {
            FakeDomainEvent domainEvent = _fixture.Create<FakeDomainEvent>();
            var envelope = new Envelope(domainEvent);
            var actual = PersistentEvent.FromEnvelope(
                envelope, new JsonMessageSerializer());
            actual.EventType.Should().Be(typeof(FakeDomainEvent).FullName);
        }

        [TestMethod]
        public void FromEnvelope_sets_MessageId_correctly()
        {
            FakeDomainEvent domainEvent = _fixture.Create<FakeDomainEvent>();
            var envelope = new Envelope(domainEvent);
            var actual = PersistentEvent.FromEnvelope(
                envelope, new JsonMessageSerializer());
            actual.MessageId.Should().Be(envelope.MessageId);
        }

        [TestMethod]
        public void FromEnvelope_sets_EventJson_correctly()
        {
            var serializer = new JsonMessageSerializer();
            FakeDomainEvent domainEvent = _fixture.Create<FakeDomainEvent>();
            var envelope = new Envelope(domainEvent);

            var actual = PersistentEvent.FromEnvelope(envelope, serializer);

            object deserialized = serializer.Deserialize(actual.EventJson);
            deserialized.Should().BeOfType<FakeDomainEvent>();
            deserialized.ShouldBeEquivalentTo(domainEvent);
        }

        [TestMethod]
        public void FromEnvelope_sets_OperationId_correctly()
        {
            FakeDomainEvent domainEvent = _fixture.Create<FakeDomainEvent>();
            var operationId = Guid.NewGuid();
            var envelope = new Envelope(Guid.NewGuid(), domainEvent, operationId);
            var actual = PersistentEvent.FromEnvelope(
                envelope, new JsonMessageSerializer());
            actual.OperationId.Should().Be(operationId);
        }

        [TestMethod]
        public void FromEnvelope_sets_CorrelationId_correctly()
        {
            FakeDomainEvent domainEvent = _fixture.Create<FakeDomainEvent>();
            var correlationId = Guid.NewGuid();
            var envelope = new Envelope(Guid.NewGuid(), domainEvent, correlationId: correlationId);
            var actual = PersistentEvent.FromEnvelope(
                envelope, new JsonMessageSerializer());
            actual.CorrelationId.Should().Be(correlationId);
        }

        [TestMethod]
        public void FromEnvelope_sets_Contributor_correctly()
        {
            FakeDomainEvent domainEvent = _fixture.Create<FakeDomainEvent>();
            string contributor = _fixture.Create<string>();
            var envelope = new Envelope(Guid.NewGuid(), domainEvent, contributor: contributor);
            var actual = PersistentEvent.FromEnvelope(
                envelope, new JsonMessageSerializer());
            actual.Contributor.Should().Be(contributor);
        }

        [TestMethod]
        public void FromEnvelope_sets_RaisedAt_correctly()
        {
            FakeDomainEvent domainEvent = _fixture.Create<FakeDomainEvent>();
            var envelope = new Envelope(domainEvent);
            var actual = PersistentEvent.FromEnvelope(
                envelope, new JsonMessageSerializer());
            actual.RaisedAt.Should().Be(domainEvent.RaisedAt);
        }
    }
}
