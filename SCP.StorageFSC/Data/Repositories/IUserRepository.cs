using SCP.StorageFSC.Data.Models;

namespace scp.filestorage.Data.Repositories
{
    public interface IUserRepository
    {
        Task<bool> InsertAsync(User user, CancellationToken cancellationToken = default);
        Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task<User?> GetByNormalizedNameAsync(string normalizedName, CancellationToken cancellationToken = default);
        Task<User?> GetByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default);
        Task<bool> UpdateAsync(User user, CancellationToken cancellationToken = default);
        Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    }
}
