namespace Khala.EventSourcing.Azure
{
    using System;
    using System.Reflection;
    using AutoFixture;
    using AutoFixture.AutoMoq;
    using AutoFixture.Idioms;
    using FluentAssertions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.WindowsAzure.Storage.Table;

    [TestClass]
    public class AggregateEntity_specs
    {
        [TestMethod]
        public void sut_is_abstract()
        {
            typeof(AggregateEntity).IsAbstract.Should().BeTrue();
        }

        [TestMethod]
        public void sut_inherits_TableEntity()
        {
            typeof(AggregateEntity).BaseType.Should().Be(typeof(TableEntity));
        }

        [TestMethod]
        public void GetPartitionKey_returns_combination_of_aggregate_type_name_and_aggregate_id()
        {
            IFixture fixture = new Fixture().Customize(new AutoMoqCustomization());
            Type aggregateType = fixture.Create<IEventSourced>().GetType();
            Guid aggregateId = fixture.Create<Guid>();

            string actual = AggregateEntity.GetPartitionKey(aggregateType, aggregateId);

            actual.Should().Be($"{aggregateType.Name}-{aggregateId:n}");
        }

        [TestMethod]
        public void GetPartitionKey_has_guard_clauses()
        {
            MethodInfo mut = typeof(AggregateEntity).GetMethod("GetPartitionKey");
            new GuardClauseAssertion(new Fixture()).Verify(mut);
        }
    }
}
