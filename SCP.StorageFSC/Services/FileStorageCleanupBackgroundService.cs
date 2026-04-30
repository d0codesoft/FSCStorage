using Microsoft.Extensions.Options;
using scp.filestorage.Data.Repositories;
using SCP.StorageFSC.Data.Repositories;

namespace scp.filestorage.Services
{
    public sealed class FileStorageCleanupBackgroundService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IOptionsMonitor<FileStorageCleanupOptions> _options;
        private readonly ILogger<FileStorageCleanupBackgroundService> _logger;

        public FileStorageCleanupBackgroundService(
            IServiceScopeFactory scopeFactory,
            IOptionsMonitor<FileStorageCleanupOptions> options,
            ILogger<FileStorageCleanupBackgroundService> logger)
        {
            _scopeFactory = scopeFactory;
            _options = options;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var initialDelay = NormalizeDelay(_options.CurrentValue.InitialDelay, TimeSpan.Zero);
            if (initialDelay > TimeSpan.Zero)
                await Task.Delay(initialDelay, stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await RunCleanupAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "File storage cleanup failed.");
                }

                var interval = NormalizeDelay(_options.CurrentValue.Interval, TimeSpan.FromDays(1));
                await Task.Delay(interval, stoppingToken);
            }
        }

        private async Task RunCleanupAsync(CancellationToken cancellationToken)
        {
            var options = _options.CurrentValue;
            if (!options.Enabled)
            {
                _logger.LogDebug("File storage cleanup is disabled.");
                return;
            }

            var nowUtc = DateTime.UtcNow;
            var completedTaskRetentionDays = Math.Max(1, options.CompletedTaskRetentionDays);
            var multipartSessionRetentionDays = Math.Max(1, options.MultipartUploadSessionRetentionDays);

            var completedTaskCutoffUtc = nowUtc.AddDays(-completedTaskRetentionDays);
            var multipartSessionCutoffUtc = nowUtc.AddDays(-multipartSessionRetentionDays);

            await using var scope = _scopeFactory.CreateAsyncScope();
            var backgroundTaskRepository = scope.ServiceProvider.GetRequiredService<IBackgroundTaskRepository>();
            var multipartUploadSessionRepository = scope.ServiceProvider.GetRequiredService<IMultipartUploadSessionRepository>();

            var deletedTasks = await backgroundTaskRepository.DeleteCompletedOlderThanAsync(
                completedTaskCutoffUtc,
                cancellationToken);

            var deletedMultipartSessions = await multipartUploadSessionRepository.DeleteTerminalOlderThanAsync(
                multipartSessionCutoffUtc,
                cancellationToken);

            _logger.LogInformation(
                "File storage cleanup completed. DeletedTasks={DeletedTasks}, DeletedMultipartSessions={DeletedMultipartSessions}, CompletedTaskCutoffUtc={CompletedTaskCutoffUtc:O}, MultipartSessionCutoffUtc={MultipartSessionCutoffUtc:O}",
                deletedTasks,
                deletedMultipartSessions,
                completedTaskCutoffUtc,
                multipartSessionCutoffUtc);
        }

        private static TimeSpan NormalizeDelay(TimeSpan value, TimeSpan fallback)
        {
            return value < TimeSpan.Zero ? fallback : value;
        }
    }
}
