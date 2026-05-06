using scp.filestorage.Data.Models;

namespace scp.filestorage.Data.Repositories
{
    public interface IUserTwoFactorChallengeRepository
    {
        Task<bool> InsertAsync(UserTwoFactorChallenge challenge, CancellationToken cancellationToken = default);
        Task<UserTwoFactorChallenge?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<UserTwoFactorChallenge>> GetPendingByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<bool> UpdateAsync(UserTwoFactorChallenge challenge, CancellationToken cancellationToken = default);
        Task<bool> DeleteExpiredAsync(DateTime utcNow, CancellationToken cancellationToken = default);
    }
}
