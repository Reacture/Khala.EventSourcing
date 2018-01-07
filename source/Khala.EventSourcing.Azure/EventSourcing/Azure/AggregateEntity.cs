namespace Khala.EventSourcing.Azure
{
    using System;
    using Microsoft.WindowsAzure.Storage.Table;

    public abstract class AggregateEntity : TableEntity
    {
        public static string GetPartitionKey(Type sourceType, Guid sourceId)
        {
            if (sourceType == null)
            {
                throw new ArgumentNullException(nameof(sourceType));
            }

            if (sourceId == Guid.Empty)
            {
                throw new ArgumentException("Value cannot be empty.", nameof(sourceId));
            }

            return $"{sourceType.Name}-{sourceId:n}";
        }
    }
}
