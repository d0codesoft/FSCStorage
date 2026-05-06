using scp.filestorage.Data.Models;

namespace scp.filestorage.Data.Repositories
{
    public interface IUserRoleRepository
    {
        Task<bool> InsertAsync(UserRole userRole, CancellationToken cancellationToken = default);

        Task<UserRole?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

        Task<UserRole?> GetByUserIdAndRoleIdAsync(
            Guid userId,
            Guid roleId,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<UserRole>> GetByUserIdAsync(
            Guid userId,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<Role>> GetRolesByUserIdAsync(
            Guid userId,
            CancellationToken cancellationToken = default);

        Task<bool> UserHasRoleAsync(
            Guid userId,
            Guid roleId,
            CancellationToken cancellationToken = default);

        Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

        Task<bool> DeleteByUserIdAndRoleIdAsync(
            Guid userId,
            Guid roleId,
            CancellationToken cancellationToken = default);

        Task<int> DeleteByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    }
}
