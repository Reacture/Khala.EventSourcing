namespace Khala.EventSourcing.Azure
{
    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.Storage.Blob;
    using Microsoft.WindowsAzure.Storage.Table;

    internal static class InternalDefaults
    {
        public static AccessCondition AccessCondition => default;

        public static BlobRequestOptions BlobRequestOptions => default;

        public static DeleteSnapshotsOption DeleteSnapshotsOption => DeleteSnapshotsOption.None;

        public static TableRequestOptions TableRequestOptions => default;

        public static OperationContext OperationContext => default;
    }
}
