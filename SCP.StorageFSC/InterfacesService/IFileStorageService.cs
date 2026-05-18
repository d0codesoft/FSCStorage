using SCP.StorageFSC.Data.Dto;
using SCP.StorageFSC.Data.Models;

namespace SCP.StorageFSC.InterfacesService
{
    /// <summary>
    /// Provides file storage operations for saving, reading, listing, and deleting files.
    /// </summary>
    public interface IFileStorageService
    {
        /// <summary>
        /// Saves a file using the specified request parameters.
        /// </summary>
        /// <param name="request">The file save request.</param>
        /// <param name="cancellationToken">The token used to cancel the operation.</param>
        /// <returns>The result of the save operation.</returns>
        Task<SaveFileResult> SaveFileAsync(
            SaveFileRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Stores a temporary file in permanent storage.
        /// </summary>
        /// <param name="tempFilePath">The path to the temporary file.</param>
        /// <param name="originalFileName">The original file name.</param>
        /// <param name="contentType">The content type of the file.</param>
        /// <param name="cancellationToken">The token used to cancel the operation.</param>
        /// <returns>The stored file entity.</returns>
        Task<StoredFile> StoreTemporaryFileAsync(
            string tempFilePath,
            string originalFileName,
            string? contentType,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves file information by file identifier.
        /// </summary>
        /// <param name="fileGuid">The file identifier.</param>
        /// <param name="cancellationToken">The token used to cancel the operation.</param>
        /// <returns>The file information if found; otherwise, <see langword="null"/>.</returns>
        Task<StoredTenantFileDto?> GetFileInfoAsync(
            Guid fileGuid,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves the list of available files.
        /// </summary>
        /// <param name="cancellationToken">The token used to cancel the operation.</param>
        /// <returns>A read-only list of stored tenant files.</returns>
        Task<IReadOnlyList<StoredTenantFileDto>> GetFilesAsync(
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Opens a file for reading.
        /// </summary>
        /// <param name="fileGuid">The file identifier.</param>
        /// <param name="cancellationToken">The token used to cancel the operation.</param>
        /// <returns>The file content result if found; otherwise, <see langword="null"/>.</returns>
        Task<FileContentResult?> OpenReadAsync(
            Guid fileGuid,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes a file by identifier.
        /// </summary>
        /// <param name="fileGuid">The file identifier.</param>
        /// <param name="cancellationToken">The token used to cancel the operation.</param>
        /// <returns><see langword="true"/> if the file was deleted; otherwise, <see langword="false"/>.</returns>
        Task<bool> DeleteFileAsync(
            Guid fileGuid,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes files that are no longer referenced.
        /// </summary>
        /// <param name="cancellationToken">The token used to cancel the operation.</param>
        /// <returns>The number of deleted orphan files.</returns>
        Task<int> DeleteOrphanFilesAsync(
            CancellationToken cancellationToken = default);
    }
}
