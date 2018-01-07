namespace Khala.EventSourcing.Azure
{
    using System;
    using System.Reflection;
    using AutoFixture;
    using AutoFixture.Idioms;
    using FluentAssertions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class Correlation_specs
    {
        [TestMethod]
        public void sut_inherits_AggregateEntity()
        {
            typeof(Correlation).BaseType.Should().Be(typeof(AggregateEntity));
        }

        [TestMethod]
        public void GetRowKey_returns_prefixed_correlation_id()
        {
            var correlationId = Guid.NewGuid();
            string actual = Correlation.GetRowKey(correlationId);
            actual.Should().Be($"Correlation-{correlationId:n}");
        }

        [TestMethod]
        public void GetRowKey_has_guard_clause()
        {
            MethodInfo mut = typeof(Correlation).GetMethod("GetRowKey");
            new GuardClauseAssertion(new Fixture()).Verify(mut);
        }

        [TestMethod]
        public void Create_returns_Correlation_instance()
        {
            Type sourceType = new Fixture().Create<Type>();
            var sourceId = Guid.NewGuid();
            var correlationId = Guid.NewGuid();

            var actual = Correlation.Create(sourceType, sourceId, correlationId);

            actual.Should().NotBeNull();
        }

        [TestMethod]
        public void Create_has_guard_clauses()
        {
            MethodInfo mut = typeof(Correlation).GetMethod("Create");
            new GuardClauseAssertion(new Fixture()).Verify(mut);
        }

        [TestMethod]
        public void Create_sets_PartitionKey_correctly()
        {
            Type sourceType = new Fixture().Create<Type>();
            var sourceId = Guid.NewGuid();
            var correlationId = Guid.NewGuid();

            var actual = Correlation.Create(sourceType, sourceId, correlationId);

            actual.PartitionKey.Should().Be(AggregateEntity.GetPartitionKey(sourceType, sourceId));
        }

        [TestMethod]
        public void Create_sets_RowKey_correctly()
        {
            Type sourceType = new Fixture().Create<Type>();
            var sourceId = Guid.NewGuid();
            var correlationId = Guid.NewGuid();

            var actual = Correlation.Create(sourceType, sourceId, correlationId);

            actual.RowKey.Should().Be(Correlation.GetRowKey(correlationId));
        }

        [TestMethod]
        public void Create_sets_CorrelationId_correctly()
        {
            Type sourceType = new Fixture().Create<Type>();
            var sourceId = Guid.NewGuid();
            var correlationId = Guid.NewGuid();

            var actual = Correlation.Create(sourceType, sourceId, correlationId);

            actual.CorrelationId.Should().Be(correlationId);
        }
    }
}
