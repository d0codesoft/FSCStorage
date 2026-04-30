using scp.filestorage.Data.Models;

namespace scp.filestorage.Data.Repositories
{
    public interface IMultipartUploadSessionRepository
    {
        Task<Guid> InsertAsync(
            MultipartUploadSession session,
            CancellationToken cancellationToken = default);

        Task<MultipartUploadSession?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default);

        Task<MultipartUploadSession?> GetByUploadIdAsync(
            Guid uploadId,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<MultipartUploadSession>> GetByTenantIdAsync(
            Guid tenantId,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<MultipartUploadSession>> GetExpiredPendingAsync(
            DateTime utcNow,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<MultipartUploadSession>> GetByStatusAsync(
            MultipartUploadStatus status,
            CancellationToken cancellationToken = default);

        Task<bool> UpdateAsync(
            MultipartUploadSession session,
            CancellationToken cancellationToken = default);

        Task<bool> UpdateStatusAsync(
            Guid id,
            MultipartUploadStatus status,
            string? errorCode = null,
            string? errorMessage = null,
            DateTime? failedAtUtc = null,
            DateTime? completedAtUtc = null,
            Guid? storedFileId = null,
            CancellationToken cancellationToken = default);

        Task<bool> TouchUpdatedAsync(
            Guid id,
            DateTime updatedUtc,
            CancellationToken cancellationToken = default);

        Task<bool> DeleteAsync(
            Guid id,
            CancellationToken cancellationToken = default);

        Task<int> DeleteTerminalOlderThanAsync(
            DateTime cutoffUtc,
            CancellationToken cancellationToken = default);
    }

}
