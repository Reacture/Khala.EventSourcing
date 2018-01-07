namespace Khala.EventSourcing.Azure
{
    using System;
    using System.Reflection;
    using AutoFixture;
    using AutoFixture.AutoMoq;
    using AutoFixture.Idioms;
    using FluentAssertions;
    using Microsoft.WindowsAzure.Storage.Table;
    using Xunit;

    public class EventEntity_specs
    {
        [Fact]
        public void sut_is_abstract()
        {
            typeof(EventEntity).IsAbstract.Should().BeTrue();
        }

        [Fact]
        public void sut_inherits_TableEntity()
        {
            typeof(EventEntity).BaseType.Should().Be(typeof(TableEntity));
        }

        [Fact]
        public void GetPartitionKey_returns_combination_of_source_type_name_and_source_id()
        {
            IFixture fixture = new Fixture().Customize(new AutoMoqCustomization());
            Type sourceType = fixture.Create<IEventSourced>().GetType();
            Guid sourceId = fixture.Create<Guid>();

            string actual = EventEntity.GetPartitionKey(sourceType, sourceId);

            actual.Should().Be($"{sourceType.Name}-{sourceId:n}");
        }

        [Fact]
        public void GetPartitionKey_has_guard_clauses()
        {
            MethodInfo mut = typeof(EventEntity).GetMethod("GetPartitionKey");
            new GuardClauseAssertion(new Fixture()).Verify(mut);
        }
    }
}
