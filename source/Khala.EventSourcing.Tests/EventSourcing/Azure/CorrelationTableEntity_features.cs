using System;
using FluentAssertions;
using Khala.FakeDomain;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.WindowsAzure.Storage.Table;

namespace Khala.EventSourcing.Azure
{
    [TestClass]
    public class CorrelationTableEntity_features
    {
        [TestMethod]
        public void class_inherits_TableEntity()
        {
            typeof(CorrelationTableEntity).BaseType.Should().Be(typeof(TableEntity));
        }

        [TestMethod]
        public void GetPartitionKey_returns_the_same_as_EventTableEntity()
        {
            var sourceType = typeof(FakeUser);
            var sourceId = Guid.NewGuid();
            string actual = CorrelationTableEntity.GetPartitionKey(sourceType, sourceId);
            actual.Should().Be(EventTableEntity.GetPartitionKey(sourceType, sourceId));
        }

        [TestMethod]
        public void RowKeyPrefix_is_Correlation()
        {
            CorrelationTableEntity.RowKeyPrefix.Should().Be("Correlation");
        }

        [TestMethod]
        public void GetRowKey_returns_combination_of_prefix_and_correlation_id()
        {
            var correlationId = Guid.NewGuid();
            string actual = CorrelationTableEntity.GetRowKey(correlationId);
            actual.Should().Be($"{CorrelationTableEntity.RowKeyPrefix}-{correlationId:n}");
        }
    }
}
