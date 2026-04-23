using SCP.StorageFSC.Data.Models;

namespace SCP.StorageFSC.Data.Repositories
{
    public interface ITenantRepository
    {
        Task<Guid> InsertAsync(Tenant tenant, CancellationToken cancellationToken = default);
        Task<Tenant?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task<Tenant?> GetByGuidAsync(Guid tenantGuid, CancellationToken cancellationToken = default);
        Task<Tenant?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<Tenant>> GetAllAsync(CancellationToken cancellationToken = default);
        Task<bool> UpdateAsync(Tenant tenant, CancellationToken cancellationToken = default);
        Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    }
}
