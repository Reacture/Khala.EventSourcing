namespace Arcane.EventSourcing.Azure
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.WindowsAzure.Storage.Table;

    internal static class InternalExtensions
    {
        public static async Task<IEnumerable<TEntity>> ExecuteQuery<TEntity>(
            this CloudTable table,
            TableQuery<TEntity> query,
            CancellationToken cancellationToken)
            where TEntity : ITableEntity, new()
        {
            var entities = new List<TEntity>();
            TableContinuationToken continuation = null;

            do
            {
                TableQuerySegment<TEntity> segment = await table
                    .ExecuteQuerySegmentedAsync(query, continuation, cancellationToken)
                    .ConfigureAwait(false);
                entities.AddRange(segment);
                continuation = segment.ContinuationToken;
            }
            while (continuation != null);

            return entities;
        }
    }
}
