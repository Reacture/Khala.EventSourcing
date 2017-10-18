namespace Khala.EventSourcing.Sql
{
    using System;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using FluentAssertions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class Aggregate_specs
    {
        [TestMethod]
        public void sut_has_SequenceId_property()
        {
            typeof(Aggregate)
                .Should()
                .HaveProperty<long>("SequenceId")
                .Which.SetMethod.IsPrivate.Should().BeTrue();
        }

        [TestMethod]
        public void SequenceId_is_decorated_with_Key()
        {
            typeof(Aggregate)
                .GetProperty("SequenceId")
                .Should()
                .BeDecoratedWith<KeyAttribute>();
        }

        [TestMethod]
        public void SequenceId_is_decorated_with_DatabaseGenerated()
        {
            typeof(Aggregate)
                .GetProperty("SequenceId")
                .Should()
                .BeDecoratedWith<DatabaseGeneratedAttribute>(a => a.DatabaseGeneratedOption == DatabaseGeneratedOption.Identity);
        }

        [TestMethod]
        public void sut_has_AggregateId_property()
        {
            typeof(Aggregate).Should().HaveProperty<Guid>("AggregateId");
        }

        [TestMethod]
        public void sut_has_AggregateType_property()
        {
            typeof(Aggregate).Should().HaveProperty<string>("AggregateType");
        }

        [TestMethod]
        public void AggregateType_is_decorated_with_Required()
        {
            typeof(Aggregate)
                .GetProperty("AggregateType")
                .Should()
                .BeDecoratedWith<RequiredAttribute>();
        }

        [TestMethod]
        public void AggregateType_is_decorated_with_StringLength()
        {
            typeof(Aggregate)
                .GetProperty("AggregateType")
                .Should()
                .BeDecoratedWith<StringLengthAttribute>(a => a.MaximumLength == 128);
        }

        [TestMethod]
        public void sut_has_Version_property()
        {
            typeof(Aggregate).Should().HaveProperty<int>("Version");
        }

        [TestMethod]
        public void Version_is_decorated_with_ConcurrencyCheck()
        {
            typeof(Aggregate)
                .GetProperty("Version")
                .Should()
                .BeDecoratedWith<ConcurrencyCheckAttribute>();
        }
    }
}
