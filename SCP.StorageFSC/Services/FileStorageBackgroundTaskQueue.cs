using System.Threading.Channels;
using SCP.StorageFSC.Data.Models;
using SCP.StorageFSC.Data.Repositories;

namespace scp.filestorage.Services
{
    public sealed class FileStorageBackgroundTaskQueue : IFileStorageBackgroundTaskQueue
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly Channel<FileStorageBackgroundTask> _queue =
            Channel.CreateUnbounded<FileStorageBackgroundTask>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false
            });

        public FileStorageBackgroundTaskQueue(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
        }

        public ValueTask QueueAsync(
            FileStorageBackgroundTask task,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(task);
            return QueueCoreAsync(task, cancellationToken);
        }

        private async ValueTask QueueCoreAsync(
            FileStorageBackgroundTask task,
            CancellationToken cancellationToken)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var repository = scope.ServiceProvider.GetRequiredService<IBackgroundTaskRepository>();

            await repository.InsertIfNotExistsAsync(
                new BackgroundTask
                {
                    TaskId = task.TaskId,
                    Type = (short)task.Type,
                    Status = BackgroundTaskStatus.Queued,
                    UploadId = task.UploadId == Guid.Empty ? null : task.UploadId,
                    QueuedAtUtc = task.CreatedAtUtc
                },
                cancellationToken);

            await _queue.Writer.WriteAsync(task, cancellationToken);
        }

        public ValueTask<FileStorageBackgroundTask> DequeueAsync(
            CancellationToken cancellationToken)
        {
            return _queue.Reader.ReadAsync(cancellationToken);
        }
    }
}
