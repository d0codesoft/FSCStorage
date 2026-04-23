using Microsoft.Extensions.Options;
using scp.filestorage.Data.Dto;
using scp.filestorage.Data.Models;
using scp.filestorage.Data.Repositories;
using scp.filestorage.InterfacesService;
using System.Security.Cryptography;

namespace scp.filestorage.Services
{
    public sealed class FileStorageMultipartService : IFileStorageMultipartService
    {
        private readonly IMultipartUploadSessionRepository _sessionRepository;
        private readonly IMultipartUploadPartRepository _partRepository;
        private readonly FileStorageMultipartOptions _options;
        private readonly ILogger<FileStorageMultipartService> _logger;

        public FileStorageMultipartService(
            IMultipartUploadSessionRepository sessionRepository,
            IMultipartUploadPartRepository partRepository,
            IOptions<FileStorageMultipartOptions> options,
            ILogger<FileStorageMultipartService> logger)
        {
            _sessionRepository = sessionRepository;
            _partRepository = partRepository;
            _options = options.Value;
            _logger = logger;

            Directory.CreateDirectory(GetRootPath());
            Directory.CreateDirectory(GetTempRootPath());
            Directory.CreateDirectory(GetFilesRootPath());
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
                PublicId = Guid.NewGuid(),
                CreatedUtc = nowUtc,
                UpdatedUtc = nowUtc,
                RowVersion = CreateRowVersion(),
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

            session.Id = await _sessionRepository.InsertAsync(session, cancellationToken);

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
                PublicId = Guid.NewGuid(),
                CreatedUtc = uploadedAtUtc,
                MultipartUploadSessionId = session.Id,
                PartNumber = request.PartNumber
            };

            part.UpdatedUtc = uploadedAtUtc;
            part.RowVersion = CreateRowVersion();
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
                part.Id = await _partRepository.InsertAsync(part, cancellationToken);
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
                var relativePath = BuildFinalRelativePath(session);
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

            EnsureSessionCanComplete(session);

            var parts = await _partRepository.GetBySessionIdAsync(session.Id, cancellationToken);
            ValidatePartsBeforeComplete(session, parts);

            await _sessionRepository.UpdateStatusAsync(
                session.Id,
                MultipartUploadStatus.Completing,
                cancellationToken: cancellationToken);

            var finalRelativePath = BuildFinalRelativePath(session);
            var finalPath = Path.Combine(GetFilesRootPath(), finalRelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(finalPath)!);

            await MergePartsAsync(session, parts, finalPath, cancellationToken);

            var fileInfo = new FileInfo(finalPath);
            if (fileInfo.Length != session.TotalFileSize)
            {
                File.Delete(finalPath);
                await FailSessionAsync(session, "complete.size_mismatch", "Merged file size mismatch.", cancellationToken);
                throw new InvalidOperationException(
                    $"Merged file size mismatch. Expected {session.TotalFileSize}, actual {fileInfo.Length}.");
            }

            var finalChecksum = await ComputeSha256Async(finalPath, cancellationToken);
            if (!string.IsNullOrWhiteSpace(session.ExpectedChecksumSha256) &&
                !string.Equals(session.ExpectedChecksumSha256, finalChecksum, StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(finalPath);
                await FailSessionAsync(session, "complete.checksum_mismatch", "Final file checksum mismatch.", cancellationToken);
                throw new InvalidOperationException("Final file checksum mismatch.");
            }

            foreach (var part in parts)
            {
                if (part.Status != MultipartUploadPartStatus.Verified)
                {
                    await _partRepository.UpdateStatusAsync(
                        part.Id,
                        MultipartUploadPartStatus.Verified,
                        uploadedAtUtc: part.UploadedAtUtc,
                        checksumSha256: part.ChecksumSha256,
                        providerPartETag: part.ProviderPartETag,
                        cancellationToken: cancellationToken);
                }
            }

            var completedAtUtc = DateTime.UtcNow;
            await _sessionRepository.UpdateStatusAsync(
                session.Id,
                MultipartUploadStatus.Completed,
                completedAtUtc: completedAtUtc,
                cancellationToken: cancellationToken);

            CleanupTempParts(session, parts);

            _logger.LogInformation(
                "Multipart upload completed. UploadId={UploadId}, FinalPath={FinalPath}",
                session.UploadId,
                finalPath);

            return new CompleteMultipartUploadResultDto
            {
                UploadId = session.UploadId,
                TenantId = session.TenantId,
                FileName = session.OriginalFileName,
                FileSize = session.TotalFileSize,
                ContentType = session.ContentType,
                FinalChecksumSha256 = finalChecksum,
                RelativePath = finalRelativePath.Replace('\\', '/'),
                PhysicalPath = finalPath,
                CompletedAtUtc = completedAtUtc,
                Status = MultipartUploadStatus.Completed,
                StoredFileId = session.StoredFileId
            };
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

        private async Task MergePartsAsync(
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

        private async Task FailSessionAsync(
            MultipartUploadSession session,
            string errorCode,
            string errorMessage,
            CancellationToken cancellationToken)
        {
            await _sessionRepository.UpdateStatusAsync(
                session.Id,
                MultipartUploadStatus.Failed,
                errorCode: errorCode,
                errorMessage: errorMessage,
                failedAtUtc: DateTime.UtcNow,
                cancellationToken: cancellationToken);
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

        private static Guid CreateRowVersion() => Guid.NewGuid();

        private static long GetExpectedPartSize(MultipartUploadSession session, int partNumber)
        {
            if (partNumber < session.TotalParts)
                return session.PartSize;

            var alreadyCovered = (long)(session.TotalParts - 1) * session.PartSize;
            return session.TotalFileSize - alreadyCovered;
        }

        private string BuildTempStoragePrefix(Guid tenantId, Guid uploadId) =>
            Path.Combine(_options.TempFolderName, tenantId.ToString("N"), uploadId.ToString("N"));

        private string BuildPartStorageKey(MultipartUploadSession session, int partNumber) =>
            Path.Combine(session.TempStoragePrefix, $"part-{partNumber:D8}.bin");

        private string BuildFinalRelativePath(MultipartUploadSession session)
        {
            var fileName = $"{session.UploadId:N}{session.Extension}";
            return Path.Combine(
                DateTime.UtcNow.ToString("yyyy"),
                DateTime.UtcNow.ToString("MM"),
                fileName);
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

        private string GetRootPath() => _options.RootPath;

        private string GetTempRootPath() => Path.Combine(GetRootPath(), _options.TempFolderName);

        private string GetFilesRootPath() => Path.Combine(GetRootPath(), _options.FilesFolderName);

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
