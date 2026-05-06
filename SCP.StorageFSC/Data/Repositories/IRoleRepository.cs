using scp.filestorage.Data.Models;

namespace scp.filestorage.Data.Repositories
{
    public interface IRoleRepository
    {
        Task<bool> InsertAsync(Role role, CancellationToken cancellationToken = default);

        Task<Role?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

        Task<Role?> GetByNormalizedNameAsync(
            string normalizedName,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<Role>> GetAllAsync(CancellationToken cancellationToken = default);

        Task<IReadOnlyList<Role>> GetSystemRolesAsync(CancellationToken cancellationToken = default);

        Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);

        Task<bool> UpdateAsync(Role role, CancellationToken cancellationToken = default);

        Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    }
}
