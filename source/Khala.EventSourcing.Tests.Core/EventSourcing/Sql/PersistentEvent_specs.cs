namespace Khala.EventSourcing.Sql
{
    using System;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using FluentAssertions;
    using Khala.Messaging;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class PersistentEvent_specs
    {
        [TestMethod]
        public void sut_has_SeqeunceId_property()
        {
            typeof(PersistentEvent)
                .Should()
                .HaveProperty<long>("SequenceId")
                .Which.SetMethod.IsPrivate.Should().BeTrue();
        }

        [TestMethod]
        public void SequenceId_is_decorated_with_Key()
        {
            typeof(PersistentEvent)
                .GetProperty("SequenceId")
                .Should()
                .BeDecoratedWith<KeyAttribute>();
        }

        [TestMethod]
        public void SequenceId_is_decorated_with_DatebaseGenerated()
        {
            typeof(PersistentEvent)
                .GetProperty("SequenceId")
                .Should()
                .BeDecoratedWith<DatabaseGeneratedAttribute>(a => a.DatabaseGeneratedOption == DatabaseGeneratedOption.Identity);
        }

        [TestMethod]
        public void sut_has_AggregateId_property()
        {
            typeof(PersistentEvent).Should().HaveProperty<Guid>("AggregateId");
        }

        [TestMethod]
        public void sut_has_Version_property()
        {
            typeof(PersistentEvent).Should().HaveProperty<int>("Version");
        }

        [TestMethod]
        public void sut_has_EventType_property()
        {
            typeof(PersistentEvent).Should().HaveProperty<string>("EventType");
        }

        [TestMethod]
        public void EventType_is_decorated_with_Required()
        {
            typeof(PersistentEvent)
                .GetProperty("EventType")
                .Should()
                .BeDecoratedWith<RequiredAttribute>(a => a.AllowEmptyStrings == false);
        }

        [TestMethod]
        public void sut_has_MessageId_prooperty()
        {
            typeof(PersistentEvent).Should().HaveProperty<Guid>("MessageId");
        }

        [TestMethod]
        public void sut_has_CorrelationId_property()
        {
            typeof(PersistentEvent).Should().HaveProperty<Guid?>("CorrelationId");
        }

        [TestMethod]
        public void sut_has_EventJson_property()
        {
            typeof(PersistentEvent).Should().HaveProperty<string>("EventJson");
        }

        [TestMethod]
        public void EventJson_is_decorated_with_Required()
        {
            typeof(PersistentEvent)
                .GetProperty("EventJson")
                .Should()
                .BeDecoratedWith<RequiredAttribute>(a => a.AllowEmptyStrings == false);
        }

        [TestMethod]
        public void sut_has_RaisedAt_property()
        {
            typeof(PersistentEvent).Should().HaveProperty<DateTimeOffset>("RaisedAt");
        }

        [TestMethod]
        public void FromEnvelope_generates_PersistentEvent_correctly()
        {
            var domainEvent = new SomeDomainEvent();
            var messageId = Guid.NewGuid();
            var correlationId = Guid.NewGuid();
            var envelope = new Envelope(messageId, correlationId, domainEvent);
            IMessageSerializer serializer = new JsonMessageSerializer();

            var actual = PersistentEvent.FromEnvelope(envelope, serializer);

            actual.AggregateId.Should().Be(domainEvent.SourceId);
            actual.Version.Should().Be(domainEvent.Version);
            actual.EventType.Should().Be(typeof(SomeDomainEvent).FullName);
            actual.MessageId.Should().Be(messageId);
            actual.CorrelationId.Should().Be(correlationId);
            actual.EventJson.Should().Be(serializer.Serialize(domainEvent));
            actual.RaisedAt.Should().Be(domainEvent.RaisedAt);
        }

        [TestMethod]
        public void FromEnvelope_has_guard_clause_for_invalid_message()
        {
            var envelope = new Envelope(new object());
            Action action = () => PersistentEvent.FromEnvelope(envelope, new JsonMessageSerializer());
            action.ShouldThrow<ArgumentException>().Where(x => x.ParamName == "envelope");
        }

        public class SomeDomainEvent : DomainEvent
        {
            private static Random _random = new Random();

            public SomeDomainEvent()
            {
                SourceId = Guid.NewGuid();
                Version = _random.Next();
                RaisedAt = DateTimeOffset.Now.AddTicks(_random.Next());
            }

            public string Content { get; set; } = Guid.NewGuid().ToString();
        }
    }
}
