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
            _container = container ?? throw new ArgumentNullException(nameof(container));
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
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

            // TODO: CancellationToken을 적용합니다.
            await blob.UploadTextAsync(_serializer.Serialize(memento)).ConfigureAwait(false);
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

            // TODO: CancellationToken을 적용합니다.
            if (await blob.ExistsAsync().ConfigureAwait(false) == false)
            {
                return null;
            }

            // TODO: CancellationToken을 적용합니다.
            using (Stream stream = await blob.OpenReadAsync().ConfigureAwait(false))
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

            // TODO: CancellationToken을 적용합니다.
            await blob.DeleteIfExistsAsync();
        }
    }
}
