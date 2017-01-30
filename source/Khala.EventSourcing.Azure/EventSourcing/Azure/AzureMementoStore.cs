namespace Khala.EventSourcing.Azure
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Messaging;
    using Microsoft.WindowsAzure.Storage.Blob;

    public class AzureMementoStore : IMementoStore
    {
        private readonly CloudBlobContainer _container;
        private readonly IMessageSerializer _serializer;

        public AzureMementoStore(
            CloudBlobContainer container,
            IMessageSerializer serializer)
        {
            if (container == null)
            {
                throw new ArgumentNullException(nameof(container));
            }

            if (serializer == null)
            {
                throw new ArgumentNullException(nameof(serializer));
            }

            _container = container;
            _serializer = serializer;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1004:GenericMethodsShouldProvideTypeParameter", Justification = "As designed.")]
        public static string GetMementoBlobName<T>(Guid sourceId)
            where T : class, IEventSourced
        {
            if (sourceId == Guid.Empty)
            {
                throw new ArgumentException(
                    $"{nameof(sourceId)} cannot be empty.", nameof(sourceId));
            }

            string s = sourceId.ToString();

            var fragments = new[]
            {
                typeof(T).FullName,
                s.Substring(0, 2),
                s.Substring(2, 2),
                $"{s}.json"
            };

            return string.Join("/", fragments);
        }

        public Task Save<T>(
            Guid sourceId,
            IMemento memento,
            CancellationToken cancellationToken)
            where T : class, IEventSourced
        {
            if (sourceId == Guid.Empty)
            {
                throw new ArgumentException(
                    $"{nameof(sourceId)} cannot be empty.", nameof(sourceId));
            }

            if (memento == null)
            {
                throw new ArgumentNullException(nameof(memento));
            }

            return SaveMemento<T>(sourceId, memento, cancellationToken);
        }

        private async Task SaveMemento<T>(
            Guid sourceId,
            IMemento memento,
            CancellationToken cancellationToken)
            where T : class, IEventSourced
        {
            string blobName = GetMementoBlobName<T>(sourceId);
            CloudBlockBlob blob = _container.GetBlockBlobReference(blobName);
            blob.Properties.ContentType = "application/json";
            await blob.UploadTextAsync(_serializer.Serialize(memento), cancellationToken).ConfigureAwait(false);
        }

        public Task<IMemento> Find<T>(
            Guid sourceId,
            CancellationToken cancellationToken)
            where T : class, IEventSourced
        {
            if (sourceId == Guid.Empty)
            {
                throw new ArgumentException(
                    $"{nameof(sourceId)} cannot be empty.", nameof(sourceId));
            }

            return FindMemento<T>(sourceId, cancellationToken);
        }

        private async Task<IMemento> FindMemento<T>(
            Guid sourceId,
            CancellationToken cancellationToken)
            where T : class, IEventSourced
        {
            string blobName = GetMementoBlobName<T>(sourceId);
            CloudBlockBlob blob = _container.GetBlockBlobReference(blobName);
            if (await blob.ExistsAsync(cancellationToken).ConfigureAwait(false) == false)
            {
                return null;
            }

            using (Stream stream = await blob.OpenReadAsync(cancellationToken).ConfigureAwait(false))
            using (var reader = new StreamReader(stream))
            {
                string content = await reader.ReadToEndAsync().ConfigureAwait(false);
                return (IMemento)_serializer.Deserialize(content);
            }
        }

        public Task Delete<T>(
            Guid sourceId,
            CancellationToken cancellationToken)
            where T : class, IEventSourced
        {
            if (sourceId == Guid.Empty)
            {
                throw new ArgumentException(
                    $"{nameof(sourceId)} cannot be empty.", nameof(sourceId));
            }

            return DeleteMemento<T>(sourceId, cancellationToken);
        }

        private async Task DeleteMemento<T>(
            Guid sourceId,
            CancellationToken cancellationToken)
            where T : class, IEventSourced
        {
            string blobName = GetMementoBlobName<T>(sourceId);
            CloudBlockBlob blob = _container.GetBlockBlobReference(blobName);
            await blob.DeleteIfExistsAsync(cancellationToken);
        }
    }
}
