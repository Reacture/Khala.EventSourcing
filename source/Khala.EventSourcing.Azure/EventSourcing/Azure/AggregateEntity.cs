namespace Khala.EventSourcing.Azure
{
    using System;
    using Microsoft.WindowsAzure.Storage.Table;

    public abstract class AggregateEntity : TableEntity
    {
        public static string GetPartitionKey(Type aggregateType, Guid aggregateId)
        {
            if (aggregateType == null)
            {
                throw new ArgumentNullException(nameof(aggregateType));
            }

            if (aggregateId == Guid.Empty)
            {
                throw new ArgumentException("Value cannot be empty.", nameof(aggregateId));
            }

            return $"{aggregateType.Name}-{aggregateId:n}";
        }
    }
}
