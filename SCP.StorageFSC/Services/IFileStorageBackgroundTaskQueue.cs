namespace scp.filestorage.Services
{
    public interface IFileStorageBackgroundTaskQueue
    {
        ValueTask QueueAsync(
            FileStorageBackgroundTask task,
            CancellationToken cancellationToken = default);

        ValueTask<FileStorageBackgroundTask> DequeueAsync(
            CancellationToken cancellationToken);
    }
}
