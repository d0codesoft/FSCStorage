using SCP.StorageFSC.Data.Models;

namespace SCP.StorageFSC.Data.Repositories
{
    public interface IUserAuthenticationAuditLogRepository
    {
        Task<Guid> InsertAsync(UserAuthenticationAuditLog log, CancellationToken cancellationToken = default);
        Task<UserAuthenticationAuditLog?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task<int> CountAsync(Guid? userId = null, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<UserAuthenticationAuditLog>> GetByUserIdAsync(Guid userId, int take = 100, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<UserAuthenticationAuditLog>> GetPagedAsync(Guid? userId, int skip, int take, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<UserAuthenticationAuditLog>> GetFailedAsync(int take = 100, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<UserAuthenticationAuditLog>> GetRecentAsync(int take = 100, CancellationToken cancellationToken = default);
    }
}
