using scp.filestorage.Data.Models;

namespace scp.filestorage.Data.Repositories
{
    public interface IMultipartUploadPartRepository
    {
        Task<Guid> InsertAsync(
            MultipartUploadPart part,
            CancellationToken cancellationToken = default);

        Task<MultipartUploadPart?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default);

        Task<MultipartUploadPart?> GetByPublicIdAsync(
            Guid publicId,
            CancellationToken cancellationToken = default);

        Task<MultipartUploadPart?> GetBySessionAndPartNumberAsync(
            Guid multipartUploadSessionId,
            int partNumber,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<MultipartUploadPart>> GetBySessionIdAsync(
            Guid multipartUploadSessionId,
            CancellationToken cancellationToken = default);

        Task<int> CountUploadedPartsAsync(
            Guid multipartUploadSessionId,
            CancellationToken cancellationToken = default);

        Task<bool> UpsertAsync(
            MultipartUploadPart part,
            CancellationToken cancellationToken = default);

        Task<bool> UpdateAsync(
            MultipartUploadPart part,
            CancellationToken cancellationToken = default);

        Task<bool> UpdateStatusAsync(
            Guid id,
            MultipartUploadPartStatus status,
            DateTime? uploadedAtUtc = null,
            string? errorMessage = null,
            int? retryCount = null,
            DateTime? lastFailedAtUtc = null,
            string? checksumSha256 = null,
            string? providerPartETag = null,
            CancellationToken cancellationToken = default);

        Task<bool> DeleteBySessionIdAsync(
            Guid multipartUploadSessionId,
            CancellationToken cancellationToken = default);

        Task<bool> DeleteAsync(
            Guid id,
            CancellationToken cancellationToken = default);
    }
}
