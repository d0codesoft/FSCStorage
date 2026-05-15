using Microsoft.Extensions.Options;
using scp.filestorage.Common;
using scp.filestorage.Data.Dto;
using scp.filestorage.Data.Models;
using scp.filestorage.Data.Repositories;
using scp.filestorage.InterfacesService;
using SCP.StorageFSC;
using System.Security.Cryptography;

namespace scp.filestorage.Services
{
    public sealed class FileStorageMultipartService : IFileStorageMultipartService
    {
        private readonly IMultipartUploadSessionRepository _sessionRepository;
        private readonly IMultipartUploadPartRepository _partRepository;
        private readonly IFileStorageBackgroundTaskQueue _backgroundTaskQueue;
        private readonly ApplicationPaths _applicationPaths;
        private readonly MultipartSettingOptions _options;
        private readonly ILogger<FileStorageMultipartService> _logger;

        public FileStorageMultipartService(
            IMultipartUploadSessionRepository sessionRepository,
            IMultipartUploadPartRepository partRepository,
            IFileStorageBackgroundTaskQueue backgroundTaskQueue,
            ApplicationPaths applicationPaths,
            IOptions<MultipartSettingOptions> options,
            ILogger<FileStorageMultipartService> logger)
        {
            _sessionRepository = sessionRepository;
            _partRepository = partRepository;
            _backgroundTaskQueue = backgroundTaskQueue;
            _applicationPaths = applicationPaths;
            _options = options.Value;
            _logger = logger;
        }

        public async Task<InitMultipartUploadResultDto> InitAsync(
            InitMultipartUploadRequestDto request,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(request.FileName))
                throw new ArgumentException("FileName is required.", nameof(request.FileName));

            if (request.FileSize <= 0)
                throw new ArgumentException("FileSize must be greater than 0.", nameof(request.FileSize));

            if (request.PartSize < _options.MinPartSizeBytes)
                throw new ArgumentException($"PartSize must be >= {_options.MinPartSizeBytes} bytes.", nameof(request.PartSize));

            if (request.PartSize > _options.MaxPartSizeBytes)
                throw new ArgumentException($"PartSize must be <= {_options.MaxPartSizeBytes} bytes.", nameof(request.PartSize));

            if (request.TenantId == Guid.Empty)
                throw new ArgumentException("TenantId is required.", nameof(request.TenantId));

            var nowUtc = DateTime.UtcNow;
            var uploadId = Guid.NewGuid();
            var originalFileName = Path.GetFileName(request.FileName);
            var normalizedFileName = Path.GetFileNameWithoutExtension(originalFileName).Trim();
            var extension = Path.GetExtension(originalFileName);
            var totalParts = checked((int)Math.Ceiling((double)request.FileSize / request.PartSize));
            var tempStoragePrefix = BuildTempStoragePrefix(request.TenantId, uploadId);

            var session = new MultipartUploadSession
            {
                CreatedUtc = nowUtc,
                UploadId = uploadId,
                TenantId = request.TenantId,
                OriginalFileName = originalFileName,
                NormalizedFileName = normalizedFileName,
                Extension = extension,
                ContentType = request.ContentType,
                TotalFileSize = request.FileSize,
                PartSize = request.PartSize,
                TotalParts = totalParts,
                ExpectedChecksumSha256 = request.ExpectedChecksumSha256,
                FinalChecksumSha256 = null,
                Status = MultipartUploadStatus.Created,
                ErrorCode = null,
                ErrorMessage = null,
                FailedAtUtc = null,
                StorageProvider = "Disk",
                TempStorageBucket = null,
                TempStoragePrefix = tempStoragePrefix,
                CompletedAtUtc = null,
                ExpiresAtUtc = request.ExpiresAtUtc,
                StoredFileId = null
            };

            var resultId = await _sessionRepository.InsertAsync(session, cancellationToken);

            Directory.CreateDirectory(GetUploadDirectory(session));

            _logger.LogInformation(
                "Multipart upload initialized. UploadId={UploadId}, TenantId={TenantId}, TotalParts={TotalParts}",
                session.UploadId,
                session.TenantId,
                session.TotalParts);

            return new InitMultipartUploadResultDto
            {
                UploadId = session.UploadId,
                TenantId = session.TenantId,
                FileName = session.OriginalFileName,
                FileSize = session.TotalFileSize,
                PartSize = session.PartSize,
                TotalParts = session.TotalParts,
                Status = session.Status,
                CreatedAtUtc = session.CreatedUtc,
                ExpiresAtUtc = session.ExpiresAtUtc
            };
        }

        public async Task<UploadMultipartPartResultDto> UploadPartAsync(
            UploadMultipartPartRequestDto request,
            CancellationToken cancellationToken = default)
        {
            if (request.Content is null || request.Content == Stream.Null)
                throw new ArgumentException("Content is required.", nameof(request.Content));

            if (request.ContentLength <= 0)
                throw new ArgumentException("ContentLength must be greater than 0.", nameof(request.ContentLength));

            var session = await _sessionRepository.GetByUploadIdAsync(request.UploadId, cancellationToken)
                          ?? throw new FileNotFoundException($"Multipart session not found for UploadId={request.UploadId}");

            EnsureSessionCanAcceptParts(session);

            if (request.PartNumber < 1 || request.PartNumber > session.TotalParts)
                throw new ArgumentOutOfRangeException(nameof(request.PartNumber));

            var expectedPartSize = GetExpectedPartSize(session, request.PartNumber);
            if (request.ContentLength != expectedPartSize)
            {
                throw new InvalidOperationException(
                    $"Invalid part size for part {request.PartNumber}. Expected {expectedPartSize}, actual {request.ContentLength}.");
            }

            var existingPart = await _partRepository.GetBySessionAndPartNumberAsync(
                session.Id,
                request.PartNumber,
                cancellationToken);

            var uploadDir = GetUploadDirectory(session);
            Directory.CreateDirectory(uploadDir);

            var storageKey = BuildPartStorageKey(session, request.PartNumber);
            var partPath = GetFullPathFromStorageKey(storageKey);
            var offset = (long)(request.PartNumber - 1) * session.PartSize;

            string? checksum = null;
            var uploadedAtUtc = DateTime.UtcNow;

            await using (var output = new FileStream(
                             partPath,
                             FileMode.Create,
                             FileAccess.Write,
                             FileShare.None,
                             bufferSize: 128 * 1024,
                             useAsync: true))
            {
                using var sha256 = SHA256.Create();
                var buffer = new byte[128 * 1024];
                long written = 0;

                while (true)
                {
                    var read = await request.Content.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
                    if (read == 0)
                        break;

                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    sha256.TransformBlock(buffer, 0, read, null, 0);
                    written += read;
                }

                sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);

                if (written != request.ContentLength)
                {
                    output.Close();
                    File.Delete(partPath);
                    throw new InvalidOperationException(
                        $"Part stream length mismatch. Expected {request.ContentLength}, actual {written}.");
                }

                checksum = Convert.ToHexString(sha256.Hash!);
            }

            if (!string.IsNullOrWhiteSpace(request.PartChecksumSha256) &&
                !string.Equals(checksum, request.PartChecksumSha256, StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(partPath);
                throw new InvalidOperationException($"Checksum mismatch for part {request.PartNumber}.");
            }

            var part = existingPart ?? new MultipartUploadPart
            {
                CreatedUtc = uploadedAtUtc,
                MultipartUploadSessionId = session.Id,
                PartNumber = request.PartNumber
            };

            part.OffsetBytes = offset;
            part.SizeInBytes = request.ContentLength;
            part.StorageKey = storageKey.Replace('\\', '/');
            part.ChecksumSha256 = checksum;
            part.ProviderPartETag = checksum;
            part.Status = MultipartUploadPartStatus.Uploaded;
            part.UploadedAtUtc = uploadedAtUtc;
            part.ErrorMessage = null;
            part.LastFailedAtUtc = null;

            if (existingPart is null)
            {
                part.RetryCount = 0;
                _ = await _partRepository.InsertAsync(part, cancellationToken);
            }
            else
            {
                part.RetryCount = existingPart.RetryCount;
                await _partRepository.UpdateAsync(part, cancellationToken);
            }

            if (session.Status == MultipartUploadStatus.Created)
            {
                await _sessionRepository.UpdateStatusAsync(
                    session.Id,
                    MultipartUploadStatus.Uploading,
                    cancellationToken: cancellationToken);
            }
            else
            {
                await _sessionRepository.TouchUpdatedAsync(session.Id, uploadedAtUtc, cancellationToken);
            }

            _logger.LogInformation(
                "Multipart part uploaded. UploadId={UploadId}, PartNumber={PartNumber}, Size={Size}",
                session.UploadId,
                part.PartNumber,
                part.SizeInBytes);

            return new UploadMultipartPartResultDto
            {
                UploadId = session.UploadId,
                PartNumber = part.PartNumber,
                OffsetBytes = part.OffsetBytes,
                SizeInBytes = part.SizeInBytes,
                StorageKey = part.StorageKey,
                ChecksumSha256 = part.ChecksumSha256,
                Status = part.Status,
                UploadedAtUtc = part.UploadedAtUtc
            };
        }

        public async Task<MultipartUploadStatusDto?> GetStatusAsync(
            Guid uploadId,
            CancellationToken cancellationToken = default)
        {
            var session = await _sessionRepository.GetByUploadIdAsync(uploadId, cancellationToken);
            if (session is null)
                return null;

            var parts = await _partRepository.GetBySessionIdAsync(session.Id, cancellationToken);
            var uploadedParts = parts
                .Where(x => x.Status is MultipartUploadPartStatus.Uploaded or MultipartUploadPartStatus.Verified)
                .OrderBy(x => x.PartNumber)
                .Select(x => x.PartNumber)
                .ToArray();
            var finalRelativePath = string.Empty;
            var finalPhysicalPath = string.Empty;

            if (!string.IsNullOrWhiteSpace(session.FinalChecksumSha256))
            {
                finalRelativePath = FileStoragePathBuilder
                    .BuildStorageRelativePath(session.FinalChecksumSha256, session.OriginalFileName)
                    .Replace('\\', '/');
                finalPhysicalPath = Path.Combine(GetFilesRootPath(), finalRelativePath);
            }

            return new MultipartUploadStatusDto
            {
                UploadId = session.UploadId,
                TenantId = session.TenantId,
                FileName = session.OriginalFileName,
                NormalizedFileName = session.NormalizedFileName,
                Extension = session.Extension,
                ContentType = session.ContentType,
                TotalFileSize = session.TotalFileSize,
                PartSize = session.PartSize,
                TotalParts = session.TotalParts,
                UploadedPartCount = uploadedParts.Length,
                UploadedParts = uploadedParts,
                Status = session.Status,
                FinalChecksumSha256 = session.FinalChecksumSha256,
                RelativePath = finalRelativePath,
                PhysicalPath = finalPhysicalPath,
                ErrorCode = session.ErrorCode,
                ErrorMessage = session.ErrorMessage,
                TempStoragePrefix = session.TempStoragePrefix,
                CreatedAtUtc = session.CreatedUtc,
                UpdatedAtUtc = session.UpdatedUtc,
                CompletedAtUtc = session.CompletedAtUtc,
                ExpiresAtUtc = session.ExpiresAtUtc,
                StoredFileId = session.StoredFileId
            };
        }

        public async Task<CompleteMultipartUploadResultDto> CompleteAsync(
            CompleteMultipartUploadRequestDto request,
            CancellationToken cancellationToken = default)
        {
            var session = await _sessionRepository.GetByUploadIdAsync(request.UploadId, cancellationToken)
                          ?? throw new FileNotFoundException($"Multipart session not found for UploadId={request.UploadId}");

            if (session.Status == MultipartUploadStatus.Completed)
            {
                var completedAt = session.CompletedAtUtc ?? session.UpdatedUtc ?? session.CreatedUtc;
                var relativePath = FileStoragePathBuilder.BuildStorageRelativePath(
                    GetCompletedSessionChecksum(session),
                    session.OriginalFileName);
                var physicalPath = Path.Combine(GetFilesRootPath(), relativePath);

                return new CompleteMultipartUploadResultDto
                {
                    UploadId = session.UploadId,
                    TenantId = session.TenantId,
                    FileName = session.OriginalFileName,
                    FileSize = session.TotalFileSize,
                    ContentType = session.ContentType,
                    FinalChecksumSha256 = session.FinalChecksumSha256,
                    RelativePath = relativePath.Replace('\\', '/'),
                    PhysicalPath = physicalPath,
                    CompletedAtUtc = completedAt,
                    Status = session.Status,
                    StoredFileId = session.StoredFileId
                };
            }

            if (session.Status == MultipartUploadStatus.Completing)
            {
                return CreateCompleteResult(session, MultipartUploadStatus.Completing);
            }

            EnsureSessionCanComplete(session);

            var parts = await _partRepository.GetBySessionIdAsync(session.Id, cancellationToken);
            ValidatePartsBeforeComplete(session, parts);

            await _sessionRepository.UpdateStatusAsync(
                session.Id,
                MultipartUploadStatus.Completing,
                cancellationToken: cancellationToken);

            await _backgroundTaskQueue.QueueAsync(
                FileStorageBackgroundTask.MergeMultipartUpload(session.UploadId),
                cancellationToken);

            _logger.LogInformation(
                "Multipart upload queued for background completion. UploadId={UploadId}",
                session.UploadId);

            session.Status = MultipartUploadStatus.Completing;
            return CreateCompleteResult(session, MultipartUploadStatus.Completing);
        }

        public async Task<AbortMultipartUploadResultDto> AbortAsync(
            Guid uploadId,
            CancellationToken cancellationToken = default)
        {
            var session = await _sessionRepository.GetByUploadIdAsync(uploadId, cancellationToken)
                          ?? throw new FileNotFoundException($"Multipart session not found for UploadId={uploadId}");

            if (session.Status == MultipartUploadStatus.Completed)
                throw new InvalidOperationException("Completed upload cannot be aborted.");

            var parts = await _partRepository.GetBySessionIdAsync(session.Id, cancellationToken);

            CleanupTempParts(session, parts);

            await _partRepository.DeleteBySessionIdAsync(session.Id, cancellationToken);
            await _sessionRepository.UpdateStatusAsync(
                session.Id,
                MultipartUploadStatus.Aborted,
                cancellationToken: cancellationToken);

            var updatedUtc = DateTime.UtcNow;

            _logger.LogInformation("Multipart upload aborted. UploadId={UploadId}", uploadId);

            return new AbortMultipartUploadResultDto
            {
                UploadId = uploadId,
                Status = MultipartUploadStatus.Aborted,
                UpdatedAtUtc = updatedUtc
            };
        }

        private static void EnsureSessionCanAcceptParts(MultipartUploadSession session)
        {
            if (session.Status is MultipartUploadStatus.Completed or MultipartUploadStatus.Aborted)
                throw new InvalidOperationException($"Upload is {session.Status}.");

            if (session.Status is MultipartUploadStatus.Failed or MultipartUploadStatus.Expired)
                throw new InvalidOperationException($"Upload is {session.Status} and cannot accept new parts.");

            if (session.ExpiresAtUtc.HasValue && session.ExpiresAtUtc.Value <= DateTime.UtcNow)
                throw new InvalidOperationException("Upload session is expired.");
        }

        private static void EnsureSessionCanComplete(MultipartUploadSession session)
        {
            if (session.Status is MultipartUploadStatus.Aborted or MultipartUploadStatus.Failed or MultipartUploadStatus.Expired)
                throw new InvalidOperationException($"Upload is {session.Status}.");

            if (session.ExpiresAtUtc.HasValue && session.ExpiresAtUtc.Value <= DateTime.UtcNow)
                throw new InvalidOperationException("Upload session is expired.");
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

        private void CleanupTempParts(
            MultipartUploadSession session,
            IReadOnlyList<MultipartUploadPart> parts)
        {
            foreach (var part in parts)
            {
                var path = GetFullPathFromStorageKey(part.StorageKey);
                TryDelete(path);
            }

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

        private string BuildTempStoragePrefix(Guid tenantId, Guid uploadId) =>
            Path.Combine(_applicationPaths.TempPath, tenantId.ToString("N"), uploadId.ToString("N"));

        private string BuildPartStorageKey(MultipartUploadSession session, int partNumber) =>
            Path.Combine(session.TempStoragePrefix, $"part-{partNumber:D8}.bin");

        private CompleteMultipartUploadResultDto CreateCompleteResult(
            MultipartUploadSession session,
            MultipartUploadStatus status)
        {
            var relativePath = string.Empty;
            var physicalPath = string.Empty;

            if (!string.IsNullOrWhiteSpace(session.FinalChecksumSha256))
            {
                relativePath = FileStoragePathBuilder
                    .BuildStorageRelativePath(session.FinalChecksumSha256, session.OriginalFileName)
                    .Replace('\\', '/');
                physicalPath = Path.Combine(GetFilesRootPath(), relativePath);
            }

            return new CompleteMultipartUploadResultDto
            {
                UploadId = session.UploadId,
                TenantId = session.TenantId,
                FileName = session.OriginalFileName,
                FileSize = session.TotalFileSize,
                ContentType = session.ContentType,
                FinalChecksumSha256 = session.FinalChecksumSha256,
                RelativePath = relativePath,
                PhysicalPath = physicalPath,
                CompletedAtUtc = session.CompletedAtUtc ?? default,
                Status = status,
                StoredFileId = session.StoredFileId
            };
        }

        private static string GetCompletedSessionChecksum(MultipartUploadSession session)
        {
            if (!string.IsNullOrWhiteSpace(session.FinalChecksumSha256))
                return session.FinalChecksumSha256;

            if (!string.IsNullOrWhiteSpace(session.ExpectedChecksumSha256))
                return session.ExpectedChecksumSha256;

            throw new InvalidOperationException(
                $"Completed multipart session '{session.UploadId}' does not have a final checksum.");
        }

        private string GetRootPath() => _applicationPaths.BasePath;

        private string GetTempRootPath() => _applicationPaths.TempPath;

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
