using SCP.StorageFSC.Data.Models;

namespace SCP.StorageFSC.Data.Repositories
{
    public interface ITenantFileRepository
    {
        Task<Guid> InsertAsync(TenantFile tenantFile, CancellationToken cancellationToken = default);
        Task<TenantFile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task<TenantFile?> GetByFileGuidAsync(Guid fileGuid, CancellationToken cancellationToken = default);
        Task<TenantFile?> GetByTenantAndFileGuidAsync(Guid tenantId, Guid fileGuid, CancellationToken cancellationToken = default);
        Task<TenantFile?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<TenantFile>> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<TenantFile>> GetByStoredFileIdAsync(Guid storedFileId, CancellationToken cancellationToken = default);
        Task<bool> SoftDeleteAsync(Guid id, DateTime deletedUtc, CancellationToken cancellationToken = default);
        Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
        Task<TenantFile?> GetByTenantAndExternalKeyAsync(Guid tenantId, string externalKey, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<TenantFile>> GetByTenantIdsAsync(IReadOnlyCollection<Guid> tenantIds, CancellationToken cancellationToken = default);
    }
}
