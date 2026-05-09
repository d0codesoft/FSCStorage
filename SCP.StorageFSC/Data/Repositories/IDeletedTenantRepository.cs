using SCP.StorageFSC.Data.Models;

namespace SCP.StorageFSC.Data.Repositories
{
    public interface IDeletedTenantRepository
    {
        Task<bool> InsertAsync(DeletedTenant deletedTenant, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<DeletedTenant>> GetPendingCleanupAsync(CancellationToken cancellationToken = default);
        Task<int> MarkCleanupCompletedAsync(
            IReadOnlyCollection<Guid> ids,
            DateTime completedUtc,
            CancellationToken cancellationToken = default);
    }
}
