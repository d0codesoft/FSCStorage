using SCP.StorageFSC.Data.Models;

namespace SCP.StorageFSC.Data.Repositories
{
    public interface IApiTokenConnectionLogRepository
    {
        Task<Guid> InsertAsync(ApiTokenConnectionLog log, CancellationToken cancellationToken = default);
        Task<ApiTokenConnectionLog?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<ApiTokenConnectionLog>> GetByApiTokenIdAsync(Guid apiTokenId, int take = 100, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<ApiTokenConnectionLog>> GetByTenantIdAsync(Guid tenantId, int take = 100, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<ApiTokenConnectionLog>> GetRecentAsync(int take = 100, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<ApiTokenConnectionLog>> GetFailedAsync(int take = 100, CancellationToken cancellationToken = default);
        Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    }
}
