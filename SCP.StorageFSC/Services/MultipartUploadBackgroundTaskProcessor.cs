using scp.filestorage.Common;
using scp.filestorage.Data.Models;
using scp.filestorage.Data.Repositories;
using SCP.StorageFSC;
using SCP.StorageFSC.Data.Models;
using SCP.StorageFSC.Data.Repositories;
using SCP.StorageFSC.InterfacesService;
using SCP.StorageFSC.Services;
using System.Security.Cryptography;

namespace scp.filestorage.Services
{
    public sealed class MultipartUploadBackgroundTaskProcessor : IMultipartUploadBackgroundTaskProcessor
    {
        private readonly IMultipartUploadSessionRepository _sessionRepository;
        private readonly IMultipartUploadPartRepository _partRepository;
        private readonly ApplicationPaths _applicationPaths;
        private readonly ILogger<MultipartUploadBackgroundTaskProcessor> _logger;
        private readonly IFileStorageService _fileStorageService;

        public MultipartUploadBackgroundTaskProcessor(
            IMultipartUploadSessionRepository sessionRepository,
            IMultipartUploadPartRepository partRepository,
            IFileStorageService fileStorageService,
            ApplicationPaths applicationPaths,
            ILogger<MultipartUploadBackgroundTaskProcessor> logger)
        {
            _sessionRepository = sessionRepository;
            _partRepository = partRepository;
            _applicationPaths = applicationPaths;
            _fileStorageService = fileStorageService;
            _logger = logger;
        }

        public async Task MergePartsAsync(
            Guid uploadId,
            CancellationToken cancellationToken = default)
        {
            var session = await _sessionRepository.GetByUploadIdAsync(uploadId, cancellationToken);
            if (session is null)
            {
                _logger.LogWarning("Multipart background merge skipped because session was not found. UploadId={UploadId}", uploadId);
                return;
            }

            if (session.Status == MultipartUploadStatus.Completed)
                return;

            if (session.Status != MultipartUploadStatus.Completing)
            {
                _logger.LogWarning(
                    "Multipart background merge skipped because session is not completing. UploadId={UploadId}, Status={Status}",
                    uploadId,
                    session.Status);
                return;
            }

            var parts = await _partRepository.GetBySessionIdAsync(session.Id, cancellationToken);
            var mergePath = Path.Combine(GetTempRootPath(), $"{session.UploadId:N}.merged");

            try
            {
                ValidatePartsBeforeComplete(session, parts);

                Directory.CreateDirectory(Path.GetDirectoryName(mergePath)!);
                await MergePartsToFileAsync(session, parts, mergePath, cancellationToken);

                var fileInfo = new FileInfo(mergePath);
                if (fileInfo.Length != session.TotalFileSize)
                {
                    throw new InvalidOperationException(
                        $"Merged file size mismatch. Expected {session.TotalFileSize}, actual {fileInfo.Length}.");
                }

                var storedFile = await _fileStorageService.StoreTemporaryFileAsync(mergePath, session.OriginalFileName, session.ContentType, cancellationToken).ConfigureAwait(false);

                session.FinalChecksumSha256 = storedFile.Sha256;
                await _sessionRepository.UpdateAsync(session, cancellationToken);
                await _sessionRepository.UpdateStatusAsync(
                    session.Id,
                    MultipartUploadStatus.Completed,
                    completedAtUtc: DateTime.UtcNow,
                    cancellationToken: cancellationToken);

                _logger.LogInformation(
                    "Multipart background merge completed. UploadId={UploadId}, FinalPath={FinalPath}",
                    session.UploadId,
                    storedFile.PhysicalPath);

                CleanupTempParts(session, parts);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                TryDelete(mergePath);
                await _sessionRepository.UpdateStatusAsync(
                    session.Id,
                    MultipartUploadStatus.Failed,
                    errorCode: "complete.background_failed",
                    errorMessage: ex.Message,
                    failedAtUtc: DateTime.UtcNow,
                    cancellationToken: cancellationToken);

                _logger.LogError(ex, "Multipart background merge failed. UploadId={UploadId}", uploadId);
            }
        }

        private async Task MergePartsToFileAsync(
            MultipartUploadSession session,
            IReadOnlyList<MultipartUploadPart> parts,
            string destinationPath,
            CancellationToken cancellationToken)
        {
            var orderedParts = parts.OrderBy(x => x.PartNumber).ToList();

            await using var destination = new FileStream(
                destinationPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 128 * 1024,
                useAsync: true);

            foreach (var part in orderedParts)
            {
                var sourcePath = GetFullPathFromStorageKey(part.StorageKey);
                if (!File.Exists(sourcePath))
                    throw new FileNotFoundException($"Part file not found: {sourcePath}");

                await using var source = new FileStream(
                    sourcePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    bufferSize: 128 * 1024,
                    useAsync: true);

                await source.CopyToAsync(destination, 128 * 1024, cancellationToken);
            }

            await destination.FlushAsync(cancellationToken);
        }

        private static void ValidatePartsBeforeComplete(
            MultipartUploadSession session,
            IReadOnlyList<MultipartUploadPart> parts)
        {
            if (parts.Count != session.TotalParts)
            {
                throw new InvalidOperationException(
                    $"Upload is incomplete. Uploaded {parts.Count} of {session.TotalParts} parts.");
            }

            var expectedNumbers = Enumerable.Range(1, session.TotalParts).ToHashSet();
            var actualNumbers = parts.Select(x => x.PartNumber).ToHashSet();

            if (!expectedNumbers.SetEquals(actualNumbers))
                throw new InvalidOperationException("Upload parts set is incomplete or contains duplicates.");

            foreach (var part in parts)
            {
                if (part.Status != MultipartUploadPartStatus.Uploaded &&
                    part.Status != MultipartUploadPartStatus.Verified)
                {
                    throw new InvalidOperationException(
                        $"Part {part.PartNumber} is not ready for completion. Status={part.Status}.");
                }

                var expectedSize = GetExpectedPartSize(session, part.PartNumber);
                if (part.SizeInBytes != expectedSize)
                {
                    throw new InvalidOperationException(
                        $"Invalid size for part {part.PartNumber}. Expected {expectedSize}, actual {part.SizeInBytes}.");
                }
            }
        }

        private async Task<string> ComputeSha256Async(string filePath, CancellationToken cancellationToken)
        {
            await using var stream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 128 * 1024,
                useAsync: true);

            using var sha = SHA256.Create();
            var hash = await sha.ComputeHashAsync(stream, cancellationToken);
            return Convert.ToHexString(hash);
        }

        private void CleanupTempParts(
            MultipartUploadSession session,
            IReadOnlyList<MultipartUploadPart> parts)
        {
            foreach (var part in parts)
            {
                var path = GetFullPathFromStorageKey(part.StorageKey);
                TryDelete(path);
            }
            _partRepository.DeleteBySessionIdAsync(session.Id).GetAwaiter().GetResult();

            var uploadDir = GetUploadDirectory(session);
            if (Directory.Exists(uploadDir))
            {
                try
                {
                    Directory.Delete(uploadDir, recursive: true);
                }
                catch
                {
                    // intentionally ignored
                }
            }
        }

        private static long GetExpectedPartSize(MultipartUploadSession session, int partNumber)
        {
            if (partNumber < session.TotalParts)
                return session.PartSize;

            var alreadyCovered = (long)(session.TotalParts - 1) * session.PartSize;
            return session.TotalFileSize - alreadyCovered;
        }

        private string GetRootPath() => _applicationPaths.BasePath;

        private string GetTempRootPath() => _applicationPaths.MultipartTempPath;

        private string GetFilesRootPath() => _applicationPaths.DataPath;

        private string GetUploadDirectory(MultipartUploadSession session) =>
            Path.Combine(GetRootPath(), session.TempStoragePrefix);

        private string GetFullPathFromStorageKey(string storageKey) =>
            Path.Combine(GetRootPath(), storageKey.Replace('/', Path.DirectorySeparatorChar));

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
                // intentionally ignored
            }
        }
    }
}
