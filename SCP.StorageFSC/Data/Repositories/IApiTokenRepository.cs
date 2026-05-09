using SCP.StorageFSC.Data.Models;

namespace SCP.StorageFSC.Data.Repositories
{
    public interface IApiTokenRepository
    {
        Task<Guid> InsertAsync(ApiToken token, CancellationToken cancellationToken = default);
        Task<ApiToken?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task<ApiToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default);
        Task<ApiToken?> GetFirstByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<ApiToken>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<ApiToken>> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<ApiToken>> GetByTenantIdAndUserIdAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
        Task<bool> UpdateAsync(ApiToken token, CancellationToken cancellationToken = default);
        Task<bool> UpdateLastUsedAsync(Guid id, DateTime lastUsedUtc, CancellationToken cancellationToken = default);
        Task<bool> UpdateLastUsedAsync(ApiToken token, CancellationToken cancellationToken = default);
        Task<bool> RevokeAsync(Guid id, DateTime revokedUtc, CancellationToken cancellationToken = default);
        Task<bool> RevokeAsync(ApiToken token, CancellationToken cancellationToken = default);
        Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
        Task<bool> HasAnyAdminTokenAsync(CancellationToken cancellationToken = default);
        Task<ApiToken?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
    }
}
