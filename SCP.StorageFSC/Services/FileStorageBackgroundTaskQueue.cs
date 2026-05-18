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

            var alreadyExists = await repository.ExistsActiveAsync(
                (short)task.Type,
                task.TenantId,
                task.ValueId == Guid.Empty ? null : task.ValueId,
                cancellationToken);

            if (alreadyExists)
                return;

            await repository.InsertIfNotExistsAsync(
                new BackgroundTask
                {
                    TaskId = task.TaskId,
                    Type = (short)task.Type,
                    Status = BackgroundTaskStatus.Queued,
                    TenantId = task.TenantId,
                    Descr = task.Descr,
                    ValueId = task.ValueId == Guid.Empty ? null : task.ValueId,
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

        public ValueTask<bool> ExistTaskAsync(
            FileStorageBackgroundTaskType type,
            Guid? tenantId,
            Guid? valueId,
            CancellationToken cancellationToken = default)
        {
            return ExistTaskInRepositoryAsync(type, tenantId, valueId, cancellationToken);
        }

        private async ValueTask<bool> ExistTaskInRepositoryAsync(
            FileStorageBackgroundTaskType type,
            Guid? tenantId,
            Guid? valueId,
            CancellationToken cancellationToken)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var repository = scope.ServiceProvider.GetRequiredService<IBackgroundTaskRepository>();

            return await repository.ExistsActiveAsync(
                (short)type,
                tenantId,
                valueId,
                cancellationToken);
        }
    }
}