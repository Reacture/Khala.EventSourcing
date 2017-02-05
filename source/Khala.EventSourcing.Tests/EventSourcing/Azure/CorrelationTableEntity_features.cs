using System;
using FluentAssertions;
using Khala.FakeDomain;
using Microsoft.WindowsAzure.Storage.Table;
using Xunit;

namespace Khala.EventSourcing.Azure
{
    public class CorrelationTableEntity_features
    {
        [Fact]
        public void class_inherits_TableEntity()
        {
            typeof(CorrelationTableEntity).BaseType.Should().Be(typeof(TableEntity));
        }

        [Fact]
        public void GetPartitionKey_returns_the_same_as_EventTableEntity()
        {
            var sourceType = typeof(FakeUser);
            var sourceId = Guid.NewGuid();
            string actual = CorrelationTableEntity.GetPartitionKey(sourceType, sourceId);
            actual.Should().Be(EventTableEntity.GetPartitionKey(sourceType, sourceId));
        }

        [Fact]
        public void RowKeyPrefix_is_Correlation()
        {
            CorrelationTableEntity.RowKeyPrefix.Should().Be("Correlation");
        }

        [Fact]
        public void GetRowKey_returns_combination_of_prefix_and_correlation_id()
        {
            var correlationId = Guid.NewGuid();
            string actual = CorrelationTableEntity.GetRowKey(correlationId);
            actual.Should().Be($"{CorrelationTableEntity.RowKeyPrefix}-{correlationId:n}");
        }
    }
}
