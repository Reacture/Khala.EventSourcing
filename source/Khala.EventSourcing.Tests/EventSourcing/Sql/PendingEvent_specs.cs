﻿namespace Khala.EventSourcing.Sql
{
    using System;
    using FluentAssertions;
    using Khala.FakeDomain.Events;
    using Khala.Messaging;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Ploeh.AutoFixture;
    using Ploeh.AutoFixture.AutoMoq;
    using Ploeh.AutoFixture.Idioms;

    [TestClass]
    public class PendingEvent_specs
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
        public void FromEnvelope_has_guard_clauses()
        {
            var builder = new Fixture().Customize(new AutoMoqCustomization());
            new GuardClauseAssertion(builder).Verify(typeof(PendingEvent).GetMethod("FromEnvelope"));
        }

        [TestMethod]
        public void FromEnvelope_sets_AggregateId_correctly()
        {
            var domainEvent = _fixture.Create<FakeUserCreated>();
            var envelope = new Envelope(domainEvent);
            var actual = PendingEvent.FromEnvelope(envelope, _serializer);
            actual.AggregateId.Should().Be(domainEvent.SourceId);
        }

        [TestMethod]
        public void FromEnvelope_sets_Version_correctly()
        {
            var domainEvent = _fixture.Create<FakeUserCreated>();
            var envelope = new Envelope(domainEvent);
            var actual = PendingEvent.FromEnvelope(envelope, _serializer);
            actual.Version.Should().Be(domainEvent.Version);
        }

        [TestMethod]
        public void FromEnvelope_sets_MessageId_correctly()
        {
            var domainEvent = _fixture.Create<FakeUserCreated>();
            var envelope = new Envelope(domainEvent);
            var actual = PendingEvent.FromEnvelope(envelope, _serializer);
            actual.MessageId.Should().Be(envelope.MessageId);
        }

        [TestMethod]
        public void FromEnvelope_sets_CorrelationId_correctly()
        {
            var domainEvent = _fixture.Create<FakeUserCreated>();
            var correlationId = Guid.NewGuid();
            var envelope = new Envelope(Guid.NewGuid(), domainEvent, correlationId: correlationId);
            var actual = PendingEvent.FromEnvelope(envelope, _serializer);
            actual.CorrelationId.Should().Be(correlationId);
        }

        [TestMethod]
        public void FromEnvelope_sets_EventJson_correctly()
        {
            var domainEvent = _fixture.Create<FakeUserCreated>();
            var envelope = new Envelope(domainEvent);

            var actual = PendingEvent.FromEnvelope(envelope, _serializer);

            object message = _serializer.Deserialize(actual.EventJson);
            message.Should().BeOfType<FakeUserCreated>();
            message.ShouldBeEquivalentTo(domainEvent);
        }

        [TestMethod]
        public void FromEnvelope_has_guard_clause_for_invalid_message()
        {
            var envelope = new Envelope(new object());
            Action action = () => PendingEvent.FromEnvelope(envelope, _serializer);
            action.ShouldThrow<ArgumentException>().Where(x => x.ParamName == "envelope");
        }
    }
}
