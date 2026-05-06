using scp.filestorage.Data.Models;

namespace scp.filestorage.Data.Repositories
{
    public interface IUserLoginChallengeRepository
    {
        Task<bool> InsertAsync(UserLoginChallenge challenge, CancellationToken cancellationToken = default);
        Task<UserLoginChallenge?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task<UserLoginChallenge?> GetByTokenHashAsync(string challengeTokenHash, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<UserLoginChallenge>> GetPendingByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<bool> UpdateAsync(UserLoginChallenge challenge, CancellationToken cancellationToken = default);
        Task<bool> DeleteExpiredAsync(DateTime utcNow, CancellationToken cancellationToken = default);
    }
}
