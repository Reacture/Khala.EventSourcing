namespace Khala.EventSourcing.Sql
{
    using System;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using FluentAssertions;
    using Xunit;

    public class Memento_specs
    {
        [Fact]
        public void sut_has_SequenceId_property()
        {
            typeof(Memento)
                .Should()
                .HaveProperty<long>("SequenceId")
                .Which.SetMethod.IsPrivate.Should().BeTrue();
        }

        [Fact]
        public void SequenceId_is_decorated_with_Key()
        {
            typeof(Memento)
                .GetProperty("SequenceId")
                .Should()
                .BeDecoratedWith<KeyAttribute>();
        }

        [Fact]
        public void SequenceId_is_decorated_with_DatabaseGenerated()
        {
            typeof(Memento)
                .GetProperty("SequenceId")
                .Should()
                .BeDecoratedWith<DatabaseGeneratedAttribute>(a => a.DatabaseGeneratedOption == DatabaseGeneratedOption.Identity);
        }

        [Fact]
        public void sut_has_AggregateId_property()
        {
            typeof(Memento).Should().HaveProperty<Guid>("AggregateId");
        }

        [Fact]
        public void sut_has_MementoJson_property()
        {
            typeof(Memento).Should().HaveProperty<string>("MementoJson");
        }

        [Fact]
        public void MementoJson_is_decorated_with_Required()
        {
            typeof(Memento)
                .GetProperty("MementoJson")
                .Should()
                .BeDecoratedWith<RequiredAttribute>(a => a.AllowEmptyStrings == false);
        }
    }
}
