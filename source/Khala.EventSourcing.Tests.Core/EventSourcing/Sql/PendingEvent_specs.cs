namespace Khala.EventSourcing.Sql
{
    using System;
    using System.ComponentModel.DataAnnotations;
    using FluentAssertions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class PendingEvent_specs
    {
        [TestMethod]
        public void sut_has_AggregateId_property()
        {
            typeof(PendingEvent).Should().HaveProperty<Guid>("AggregateId");
        }

        [TestMethod]
        public void sut_has_Version_property()
        {
            typeof(PendingEvent).Should().HaveProperty<int>("Version");
        }

        [TestMethod]
        public void sut_has_MessageId_property()
        {
            typeof(PendingEvent).Should().HaveProperty<Guid>("MessageId");
        }

        [TestMethod]
        public void sut_has_OperationId_property()
        {
            typeof(PendingEvent).Should().HaveProperty<string>("OperationId");
        }

        [TestMethod]
        public void OperationId_is_decorated_with_StringLength()
        {
            typeof(PendingEvent)
                .GetProperty("OperationId")
                .Should()
                .BeDecoratedWith<StringLengthAttribute>(a => a.MaximumLength == 100);
        }

        [TestMethod]
        public void sut_has_CorrelationId_property()
        {
            typeof(PendingEvent).Should().HaveProperty<Guid?>("CorrelationId");
        }

        [TestMethod]
        public void sut_has_EventJson_property()
        {
            typeof(PendingEvent).Should().HaveProperty<string>("EventJson");
        }

        [TestMethod]
        public void EventJson_is_decorated_with_Required()
        {
            typeof(PendingEvent)
                .GetProperty("EventJson")
                .Should()
                .BeDecoratedWith<RequiredAttribute>(a => a.AllowEmptyStrings == false);
        }

        public class SomeDomainEvent : DomainEvent
        {
            private static Random _random = new Random();

            public SomeDomainEvent()
            {
                SourceId = Guid.NewGuid();
                Version = _random.Next();
                RaisedAt = DateTime.UtcNow.AddTicks(_random.Next());
            }

            public string Content { get; set; } = Guid.NewGuid().ToString();
        }
    }
}
