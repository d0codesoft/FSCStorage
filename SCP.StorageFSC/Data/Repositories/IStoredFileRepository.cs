using SCP.StorageFSC.Data.Models;

namespace SCP.StorageFSC.Data.Repositories
{
    public interface IStoredFileRepository
    {
        Task<Guid> InsertAsync(StoredFile file, CancellationToken cancellationToken = default);
        Task<StoredFile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task<StoredFile?> GetBySha256Async(string sha256, CancellationToken cancellationToken = default);
        Task<StoredFile?> GetByHashesAsync(string sha256, string crc32, CancellationToken cancellationToken = default);
        Task<bool> IncrementReferenceCountAsync(Guid id, CancellationToken cancellationToken = default);
        Task<bool> DecrementReferenceCountAsync(Guid id, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<StoredFile>> GetOrphanFilesAsync(CancellationToken cancellationToken = default);
        Task<bool> MarkDeletedAsync(Guid id, DateTime deletedUtc, CancellationToken cancellationToken = default);
        Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    }
}
