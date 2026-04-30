namespace scp.filestorage.Services
{
    using scp.filestorage.Data.Models;
    using scp.filestorage.Data.Repositories;
    using SCP.StorageFSC.Data.Repositories;

    public sealed class FileStorageBackgroundService : BackgroundService
    {
        private readonly IFileStorageBackgroundTaskQueue _queue;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<FileStorageBackgroundService> _logger;

        public FileStorageBackgroundService(
            IFileStorageBackgroundTaskQueue queue,
            IServiceScopeFactory scopeFactory,
            ILogger<FileStorageBackgroundService> logger)
        {
            _queue = queue;
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await ResumeCompletingMultipartUploadsAsync(stoppingToken);
            await QueueDatabaseConsistencyCheckAsync(stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                FileStorageBackgroundTask task;
                try
                {
                    task = await _queue.DequeueAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }

                await ExecuteTaskAsync(task, stoppingToken);
            }
        }

        private async Task ResumeCompletingMultipartUploadsAsync(CancellationToken cancellationToken)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var sessions = await scope.ServiceProvider
                    .GetRequiredService<IMultipartUploadSessionRepository>()
                    .GetByStatusAsync(MultipartUploadStatus.Completing, cancellationToken);

                foreach (var session in sessions)
                {
                    await _queue.QueueAsync(
                        FileStorageBackgroundTask.MergeMultipartUpload(session.UploadId),
                        cancellationToken);
                }

                if (sessions.Count > 0)
                {
                    _logger.LogInformation(
                        "Queued completing multipart uploads after startup. Count={Count}",
                        sessions.Count);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to queue completing multipart uploads after startup.");
            }
        }

        private async Task QueueDatabaseConsistencyCheckAsync(CancellationToken cancellationToken)
        {
            try
            {
                await _queue.QueueAsync(
                    FileStorageBackgroundTask.CheckDatabaseConsistency(),
                    cancellationToken);

                _logger.LogInformation("Queued file storage database consistency check.");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to queue file storage database consistency check.");
            }
        }

        private async Task ExecuteTaskAsync(
            FileStorageBackgroundTask task,
            CancellationToken cancellationToken)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var taskRepository = scope.ServiceProvider.GetRequiredService<IBackgroundTaskRepository>();

            try
            {
                await taskRepository.MarkRunningAsync(task.TaskId, DateTime.UtcNow, cancellationToken);

                string? resultSummary = null;
                switch (task.Type)
                {
                    case FileStorageBackgroundTaskType.MergeMultipartUpload:
                        var processor = scope.ServiceProvider.GetRequiredService<IMultipartUploadBackgroundTaskProcessor>();
                        await processor.MergePartsAsync(task.UploadId, cancellationToken);
                        resultSummary = $"Multipart upload {task.UploadId} merged.";
                        break;

                    case FileStorageBackgroundTaskType.CheckDatabaseConsistency:
                        var consistencyService = scope.ServiceProvider.GetRequiredService<IFileStorageConsistencyService>();
                        var result = await consistencyService.CheckAsync(cancellationToken);
                        resultSummary = $"IsConsistent={result.IsConsistent}; CheckedFiles={result.CheckedFiles}; Issues={result.Issues.Count}";
                        _logger.LogInformation(
                            "Database consistency task finished. TaskId={TaskId}, IsConsistent={IsConsistent}, CheckedFiles={CheckedFiles}, Issues={IssueCount}",
                            task.TaskId,
                            result.IsConsistent,
                            result.CheckedFiles,
                            result.Issues.Count);
                        break;

                    default:
                        _logger.LogWarning("Unknown file storage background task type. TaskId={TaskId}, Type={TaskType}", task.TaskId, task.Type);
                        resultSummary = $"Unknown task type: {task.Type}.";
                        break;
                }

                await taskRepository.MarkCompletedAsync(task.TaskId, DateTime.UtcNow, resultSummary, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                await taskRepository.MarkFailedAsync(task.TaskId, DateTime.UtcNow, "Application shutdown canceled the task.", CancellationToken.None);
                throw;
            }
            catch (Exception ex)
            {
                await taskRepository.MarkFailedAsync(task.TaskId, DateTime.UtcNow, ex.Message, CancellationToken.None);
                _logger.LogError(
                    ex,
                    "File storage background task failed. TaskId={TaskId}, Type={TaskType}, UploadId={UploadId}",
                    task.TaskId,
                    task.Type,
                    task.UploadId);
            }
        }
    }
}
