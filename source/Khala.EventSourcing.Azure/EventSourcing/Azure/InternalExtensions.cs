namespace Khala.EventSourcing.Azure
{
    using System.Collections.Generic;
    using System.Linq;
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
                // TODO: CancellationToken을 적용합니다.
                TableQuerySegment<TEntity> segment = await table
                    .ExecuteQuerySegmentedAsync(query, continuation)
                    .ConfigureAwait(false);
                entities.AddRange(segment);
                continuation = segment.ContinuationToken;
            }
            while (continuation != null);

            return entities;
        }

        public static async Task<bool> Any<TEntity>(
            this CloudTable table,
            TableQuery<TEntity> query,
            CancellationToken cancellationToken)
            where TEntity : ITableEntity, new()
        {
            IEnumerable<TEntity> entities = await
                table.ExecuteQuery(query.Take(1), cancellationToken);

            return entities.Any();
        }
    }
}
