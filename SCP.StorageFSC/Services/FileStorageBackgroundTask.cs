namespace scp.filestorage.Services
{
    public enum FileStorageBackgroundTaskType
    {
        MergeMultipartUpload = 0,
        CheckDatabaseConsistency = 1
    }

    public sealed record FileStorageBackgroundTask(
        Guid TaskId,
        FileStorageBackgroundTaskType Type,
        Guid UploadId,
        DateTime CreatedAtUtc)
    {
        public static FileStorageBackgroundTask MergeMultipartUpload(Guid uploadId) =>
            new(Guid.NewGuid(), FileStorageBackgroundTaskType.MergeMultipartUpload, uploadId, DateTime.UtcNow);

        public static FileStorageBackgroundTask CheckDatabaseConsistency() =>
            new(Guid.NewGuid(), FileStorageBackgroundTaskType.CheckDatabaseConsistency, Guid.Empty, DateTime.UtcNow);
    }
}
