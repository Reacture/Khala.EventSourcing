namespace Khala.EventSourcing.Azure
{
    using System;
    using System.Reflection;
    using AutoFixture;
    using AutoFixture.Idioms;
    using FluentAssertions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class CorrelationEntity_specs
    {
        [TestMethod]
        public void sut_inherits_AggregateEntity()
        {
            typeof(CorrelationEntity).BaseType.Should().Be(typeof(AggregateEntity));
        }

        [TestMethod]
        public void GetRowKey_returns_prefixed_correlation_id()
        {
            var correlationId = Guid.NewGuid();
            string actual = CorrelationEntity.GetRowKey(correlationId);
            actual.Should().Be($"Correlation-{correlationId:n}");
        }

        [TestMethod]
        public void GetRowKey_has_guard_clause()
        {
            MethodInfo mut = typeof(CorrelationEntity).GetMethod("GetRowKey");
            new GuardClauseAssertion(new Fixture()).Verify(mut);
        }

        [TestMethod]
        public void Create_returns_CorrelationEntity_instance()
        {
            Type aggregateType = new Fixture().Create<Type>();
            var aggregateId = Guid.NewGuid();
            var correlationId = Guid.NewGuid();

            var actual = CorrelationEntity.Create(aggregateType, aggregateId, correlationId);

            actual.Should().NotBeNull();
        }

        [TestMethod]
        public void Create_has_guard_clauses()
        {
            MethodInfo mut = typeof(CorrelationEntity).GetMethod("Create");
            new GuardClauseAssertion(new Fixture()).Verify(mut);
        }

        [TestMethod]
        public void Create_sets_PartitionKey_correctly()
        {
            Type aggregateType = new Fixture().Create<Type>();
            var aggregateId = Guid.NewGuid();
            var correlationId = Guid.NewGuid();

            var actual = CorrelationEntity.Create(aggregateType, aggregateId, correlationId);

            actual.PartitionKey.Should().Be(AggregateEntity.GetPartitionKey(aggregateType, aggregateId));
        }

        [TestMethod]
        public void Create_sets_RowKey_correctly()
        {
            Type aggregateType = new Fixture().Create<Type>();
            var aggregateId = Guid.NewGuid();
            var correlationId = Guid.NewGuid();

            var actual = CorrelationEntity.Create(aggregateType, aggregateId, correlationId);

            actual.RowKey.Should().Be(CorrelationEntity.GetRowKey(correlationId));
        }

        [TestMethod]
        public void Create_sets_CorrelationId_correctly()
        {
            Type aggregateType = new Fixture().Create<Type>();
            var aggregateId = Guid.NewGuid();
            var correlationId = Guid.NewGuid();

            var actual = CorrelationEntity.Create(aggregateType, aggregateId, correlationId);

            actual.CorrelationId.Should().Be(correlationId);
        }
    }
}
