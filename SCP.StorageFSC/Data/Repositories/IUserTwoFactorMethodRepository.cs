using scp.filestorage.Data.Models;

namespace scp.filestorage.Data.Repositories
{
    public interface IUserTwoFactorMethodRepository
    {
        Task<bool> InsertAsync(UserTwoFactorMethod method, CancellationToken cancellationToken = default);
        Task<UserTwoFactorMethod?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<UserTwoFactorMethod>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<UserTwoFactorMethod?> GetDefaultAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<UserTwoFactorMethod?> GetByUserAndTypeAsync(Guid userId, TwoFactorMethodType methodType, CancellationToken cancellationToken = default);
        Task<bool> UpdateAsync(UserTwoFactorMethod method, CancellationToken cancellationToken = default);
        Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    }
}
