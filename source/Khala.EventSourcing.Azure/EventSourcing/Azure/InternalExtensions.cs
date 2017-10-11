namespace Khala.EventSourcing.Azure
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.WindowsAzure.Storage.Blob;
    using Microsoft.WindowsAzure.Storage.Table;

    internal static class InternalExtensions
    {
        public static Task<bool> Exists(
            this CloudBlob blob,
            CancellationToken cancellationToken)
        {
            return blob.ExistsAsync(
                InternalDefaults.BlobRequestOptions,
                InternalDefaults.OperationContext,
                cancellationToken);
        }

        public static Task<Stream> OpenRead(
            this CloudBlob blob,
            CancellationToken cancellationToken)
        {
            return blob.OpenReadAsync(
                InternalDefaults.AccessCondition,
                InternalDefaults.BlobRequestOptions,
                InternalDefaults.OperationContext,
                cancellationToken);
        }

        public static Task UploadText(
            this CloudBlockBlob blockBlob,
            string content,
            CancellationToken cancellationToken)
        {
            return blockBlob.UploadTextAsync(
                content,
                Encoding.UTF8,
                InternalDefaults.AccessCondition,
                InternalDefaults.BlobRequestOptions,
                InternalDefaults.OperationContext,
                cancellationToken);
        }

        public static Task<bool> DeleteIfExists(
            this CloudBlob blob,
            CancellationToken cancellationToken)
        {
            return blob.DeleteIfExistsAsync(
                InternalDefaults.DeleteSnapshotsOption,
                InternalDefaults.AccessCondition,
                InternalDefaults.BlobRequestOptions,
                InternalDefaults.OperationContext,
                cancellationToken);
        }

        public static Task<TableQuerySegment<TEntity>> ExecuteQuerySegmented<TEntity>(
            this CloudTable table,
            TableQuery<TEntity> query,
            TableContinuationToken token,
            CancellationToken cancellationToken)
            where TEntity : ITableEntity, new()
        {
            return table.ExecuteQuerySegmentedAsync(
                query,
                token,
                InternalDefaults.TableRequestOptions,
                InternalDefaults.OperationContext,
                cancellationToken);
        }

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
                    .ExecuteQuerySegmented(query, continuation, cancellationToken)
                    .ConfigureAwait(false);
                entities.AddRange(segment);
                continuation = segment.ContinuationToken;
            }
            while (continuation != null);

            return entities;
        }

        public static Task<bool> Any<TEntity>(
            this CloudTable table,
            TableQuery<TEntity> query,
            CancellationToken cancellationToken)
            where TEntity : ITableEntity, new()
        {
            return table
                .ExecuteQuery(query.Take(1), cancellationToken)
                .ContinueWith(task => task.Result.Any());
        }

        public static Task Execute(
            this CloudTable table,
            TableOperation operation,
            CancellationToken cancellationToken)
        {
            return table.ExecuteAsync(
                operation,
                InternalDefaults.TableRequestOptions,
                InternalDefaults.OperationContext,
                cancellationToken);
        }

        public static Task ExecuteBatch(
            this CloudTable table,
            TableBatchOperation batch,
            CancellationToken cancellationToken)
        {
            return table.ExecuteBatchAsync(
                batch,
                InternalDefaults.TableRequestOptions,
                InternalDefaults.OperationContext,
                cancellationToken);
        }
    }
}
