namespace Khala.EventSourcing.Sql
{
    using System;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using FluentAssertions;
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
        public void sut_has_MessageId_property()
        {
            typeof(PersistentEvent).Should().HaveProperty<Guid>("MessageId");
        }

        [TestMethod]
        public void sut_has_OperationId_property()
        {
            typeof(PersistentEvent).Should().HaveProperty<string>("OperationId");
        }

        [TestMethod]
        public void OperationId_is_decorated_with_StringLength()
        {
            typeof(PersistentEvent)
                .GetProperty("OperationId")
                .Should()
                .BeDecoratedWith<StringLengthAttribute>(a => a.MaximumLength == 100);
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
            typeof(PersistentEvent).Should().HaveProperty<DateTime>("RaisedAt");
        }
    }
}
