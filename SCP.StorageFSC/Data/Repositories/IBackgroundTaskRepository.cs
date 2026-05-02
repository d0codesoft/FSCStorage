using SCP.StorageFSC.Data.Models;

namespace SCP.StorageFSC.Data.Repositories
{
    public interface IBackgroundTaskRepository
    {
        Task<Guid> InsertIfNotExistsAsync(
            BackgroundTask task,
            CancellationToken cancellationToken = default);

        Task<BackgroundTask?> GetByTaskIdAsync(
            Guid taskId,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<BackgroundTask>> GetActiveAsync(
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<BackgroundTask>> GetCompletedAsync(
            int limit = 100,
            CancellationToken cancellationToken = default);

        Task<bool> MarkRunningAsync(
            Guid taskId,
            DateTime startedAtUtc,
            CancellationToken cancellationToken = default);

        Task<bool> MarkCompletedAsync(
            Guid taskId,
            DateTime completedAtUtc,
            string? resultSummary = null,
            CancellationToken cancellationToken = default);

        Task<bool> MarkFailedAsync(
            Guid taskId,
            DateTime failedAtUtc,
            string errorMessage,
            CancellationToken cancellationToken = default);

        Task<bool> MarkCanceledAsync(
            Guid taskId,
            DateTime canceledAtUtc,
            string? resultSummary = null,
            CancellationToken cancellationToken = default);

        Task<int> DeleteCompletedOlderThanAsync(
            DateTime cutoffUtc,
            CancellationToken cancellationToken = default);
    }
}
