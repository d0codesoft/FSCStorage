using scp.filestorage.Data.Models;

namespace scp.filestorage.Data.Repositories
{
    public interface IUserRecoveryCodeRepository
    {
        Task<bool> InsertAsync(UserRecoveryCode code, CancellationToken cancellationToken = default);
        Task<int> InsertManyAsync(IEnumerable<UserRecoveryCode> codes, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<UserRecoveryCode>> GetUnusedByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<UserRecoveryCode?> GetUnusedByHashAsync(Guid userId, string codeHash, CancellationToken cancellationToken = default);
        Task<bool> MarkUsedAsync(Guid id, DateTime usedUtc, string? ipAddress, string? userAgent, CancellationToken cancellationToken = default);
        Task<int> DeleteByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    }
}
