using SCP.StorageFSC.Data.Dto;

namespace SCP.StorageFSC.InterfacesService
{
    public interface IFileStorageService
    {
        Task<SaveFileResult> SaveFileAsync(
            SaveFileRequest request,
            CancellationToken cancellationToken = default);

        Task<StoredTenantFileDto?> GetFileInfoAsync(
            Guid fileGuid,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<StoredTenantFileDto>> GetFilesAsync(
            CancellationToken cancellationToken = default);

        Task<FileContentResult?> OpenReadAsync(
            Guid fileGuid,
            CancellationToken cancellationToken = default);

        Task<bool> DeleteFileAsync(
            Guid fileGuid,
            CancellationToken cancellationToken = default);

        Task<int> DeleteOrphanFilesAsync(
            CancellationToken cancellationToken = default);
    }
}
