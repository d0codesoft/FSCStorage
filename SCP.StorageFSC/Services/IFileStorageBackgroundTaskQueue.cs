namespace scp.filestorage.Services
{
    public interface IFileStorageBackgroundTaskQueue
    {
        ValueTask QueueAsync(
            FileStorageBackgroundTask task,
            CancellationToken cancellationToken = default);

        ValueTask<FileStorageBackgroundTask> DequeueAsync(
            CancellationToken cancellationToken);

        /// <summary>
        /// Returns <c>true</c> if an active (not Completed, Failed, or Canceled) task
        /// with the specified <paramref name="type"/>, <paramref name="tenantId"/>,
        /// and <paramref name="valueId"/> already exists in the queue.
        /// </summary>
        ValueTask<bool> ExistTaskAsync(
            FileStorageBackgroundTaskType type,
            Guid? tenantId,
            Guid? valueId,
            CancellationToken cancellationToken = default);
    }
}
