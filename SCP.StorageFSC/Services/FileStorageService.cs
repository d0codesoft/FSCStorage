using Microsoft.Extensions.Options;
using SCP.StorageFSC.Data.Dto;
using SCP.StorageFSC.Data.Models;
using SCP.StorageFSC.Data.Repositories;
using SCP.StorageFSC.InterfacesService;
using SCP.StorageFSC.Security;
using SCP.StorageFSC.SecurityPermission;
using System.Runtime.Intrinsics.Arm;
using System.Security.Cryptography;

namespace SCP.StorageFSC.Services
{
    public sealed class FileStorageService : IFileStorageService
    {
        private readonly ITenantFileRepository _tenantFileRepository;
        private readonly IStoredFileRepository _storedFileRepository;
        private readonly ICurrentTenantAccessor _currentTenantAccessor;
        private readonly ITenantAuthorizationService _tenantAuthorizationService;
        private readonly FileStorageOptions _options;
        private readonly ILogger<FileStorageService> _logger;

        public FileStorageService(
            ITenantFileRepository tenantFileRepository,
            IStoredFileRepository storedFileRepository,
            ICurrentTenantAccessor currentTenantAccessor,
            ITenantAuthorizationService tenantAuthorizationService,
            IOptions<FileStorageOptions> options,
            ILogger<FileStorageService> logger)
        {
            _tenantFileRepository = tenantFileRepository;
            _storedFileRepository = storedFileRepository;
            _currentTenantAccessor = currentTenantAccessor;
            _tenantAuthorizationService = tenantAuthorizationService;
            _options = options.Value;
            _logger = logger;
        }

        public async Task<SaveFileResult> SaveFileAsync(
            SaveFileRequest request,
            CancellationToken cancellationToken = default)
        {
            _tenantAuthorizationService.DemandPermission(TenantPermission.Write);

            var current = _currentTenantAccessor.GetRequired();
            var tenantId = GetRequiredTenantId(current);

            if (request.Content is null || request.Content == Stream.Null)
            {
                _logger.LogWarning(
                    "File content is null or empty. TenantId={TenantId}, FileName={FileName}",
                    current.TenantId,
                    request.FileName);
                return SaveFileResult.Fail(
                        SaveFileStatus.ValidationError,
                        "validation.content_required",
                        "File content is required.");
            }

            if (!current.CanWrite && !current.IsAdmin)
            {
                return SaveFileResult.Fail(
                    SaveFileStatus.AccessDenied,
                    "auth.write_denied",
                    "Write permission is required.");
            }

            if (string.IsNullOrWhiteSpace(request.FileName))
            {
                _logger.LogWarning(
                    "File name is null or empty. TenantId={TenantId}, FileName={FileName}",
                    current.TenantId,
                    request.FileName);
                return SaveFileResult.Fail(
                    SaveFileStatus.ValidationError,
                    "validation.file_name_required",
                    "File name is required.");
            }

            var tempPath = Path.Combine(_options.DataPath, $".tmp_{Guid.NewGuid():N}");
            string sha256;
            string crc32;
            long fileSize;

            try
            {
                (sha256, crc32, fileSize) = await SaveToTempAndCalculateHashesAsync(
                    request.Content,
                    tempPath,
                    cancellationToken);

                var existingStoredFile = await _storedFileRepository.GetByHashesAsync(
                    sha256,
                    crc32,
                    cancellationToken);

                StoredFile storedFile;
                if (existingStoredFile is not null)
                {
                    storedFile = existingStoredFile;

                    await _storedFileRepository.IncrementReferenceCountAsync(
                        storedFile.Id,
                        cancellationToken);

                    TryDeleteFile(tempPath);

                    _logger.LogInformation(
                        "Deduplicated file detected. StoredFileId={StoredFileId}, TenantId={TenantId}, Sha256={Sha256}",
                        storedFile.Id,
                        current.TenantId,
                        storedFile.Sha256);
                }
                else
                {
                    var finalRelativePath = BuildStorageRelativePath(sha256, request.FileName);
                    var finalFullPath = Path.Combine(_options.DataPath, finalRelativePath);

                    var finalDirectory = Path.GetDirectoryName(finalFullPath);
                    if (!string.IsNullOrWhiteSpace(finalDirectory))
                    {
                        Directory.CreateDirectory(finalDirectory);
                    }

                    if (File.Exists(finalFullPath))
                    {
                        if (TryDeleteFile(tempPath))
                        {
                            _logger.LogWarning("Failed to delete file at path: {Path}", tempPath);
                        }
                        else
                        {
                            _logger.LogWarning("File already exists at destination but failed to delete temp file. TempPath={TempPath}, Destination={Destination}", tempPath, finalFullPath);
                        }

                    }
                    else
                    {
                        if (!TryMoveFile(tempPath, finalFullPath))
                        {
                            _logger.LogWarning("Failed to move file from {Path} to {Destination}", tempPath, finalFullPath);
                        }
                    }

                    storedFile = new StoredFile
                    {
                        Sha256 = sha256,
                        Crc32 = crc32,
                        FileSize = fileSize,
                        PhysicalPath = finalRelativePath.Replace('\\', '/'),
                        OriginalFileName = request.FileName,
                        ContentType = request.ContentType,
                        ReferenceCount = 1,
                        CreatedUtc = DateTime.UtcNow,
                        IsDeleted = false
                    };

                    storedFile.Id = await _storedFileRepository.InsertAsync(
                        storedFile,
                        cancellationToken);

                    _logger.LogInformation(
                        "Physical file stored. StoredFileId={StoredFileId}, TenantId={TenantId}, Path={PhysicalPath}, Size={FileSize}",
                        storedFile.Id,
                        tenantId,
                        storedFile.PhysicalPath,
                        storedFile.FileSize);
                }

                var tenantFile = new TenantFile
                {
                    TenantId = tenantId,
                    StoredFileId = storedFile.Id,
                    FileGuid = Guid.NewGuid(),
                    FileName = request.FileName,
                    Category = request.Category,
                    ExternalKey = request.ExternalKey,
                    IsActive = true,
                    CreatedUtc = DateTime.UtcNow
                };

                tenantFile.Id = await _tenantFileRepository.InsertAsync(
                    tenantFile,
                    cancellationToken);

                _logger.LogInformation(
                    "Tenant file link created. TenantFileId={TenantFileId}, TenantId={TenantId}, StoredFileId={StoredFileId}, FileGuid={FileGuid}",
                    tenantFile.Id,
                    tenantFile.TenantId,
                    tenantFile.StoredFileId,
                    tenantFile.FileGuid);

                return SaveFileResult.Ok(Map(tenantFile, storedFile));
            }
            catch
            {
                TryDeleteFile(tempPath);
                return SaveFileResult.Fail(
                    SaveFileStatus.StorageFailed,
                    "storage.failed",
                    "Failed to store the file.");
            }
        }

        public async Task<StoredTenantFileDto?> GetFileInfoAsync(
            Guid fileGuid,
            CancellationToken cancellationToken = default)
        {
            _tenantAuthorizationService.DemandPermission(TenantPermission.Read);

            var current = _currentTenantAccessor.GetRequired();
            var tenantId = GetRequiredTenantId(current);

            var tenantFile = await _tenantFileRepository.GetByTenantAndFileGuidAsync(
                tenantId,
                fileGuid,
                cancellationToken);

            if (tenantFile is null)
                return null;

            var storedFile = await _storedFileRepository.GetByIdAsync(
                tenantFile.StoredFileId,
                cancellationToken);

            if (storedFile is null || storedFile.IsDeleted)
                return null;

            return Map(tenantFile, storedFile);
        }

        public async Task<IReadOnlyList<StoredTenantFileDto>> GetFilesAsync(
            CancellationToken cancellationToken = default)
        {
            _tenantAuthorizationService.DemandPermission(TenantPermission.Read);

            var current = _currentTenantAccessor.GetRequired();
            var tenantId = GetRequiredTenantId(current);

            var tenantFiles = await _tenantFileRepository.GetByTenantIdAsync(
                tenantId,
                cancellationToken);

            var result = new List<StoredTenantFileDto>(tenantFiles.Count);

            foreach (var tenantFile in tenantFiles)
            {
                var storedFile = await _storedFileRepository.GetByIdAsync(
                    tenantFile.StoredFileId,
                    cancellationToken);

                if (storedFile is null || storedFile.IsDeleted)
                    continue;

                result.Add(Map(tenantFile, storedFile));
            }

            return result;
        }

        public async Task<FileContentResult?> OpenReadAsync(
            Guid fileGuid,
            CancellationToken cancellationToken = default)
        {
            _tenantAuthorizationService.DemandPermission(TenantPermission.Read);

            var current = _currentTenantAccessor.GetRequired();
            var tenantId = GetRequiredTenantId(current);

            var tenantFile = await _tenantFileRepository.GetByTenantAndFileGuidAsync(
                tenantId,
                fileGuid,
                cancellationToken);

            if (tenantFile is null)
                return null;

            var storedFile = await _storedFileRepository.GetByIdAsync(
                tenantFile.StoredFileId,
                cancellationToken);

            if (storedFile is null || storedFile.IsDeleted)
                return null;

            var fullPath = GetFullPhysicalPath(storedFile.PhysicalPath);
            if (!File.Exists(fullPath))
            {
                _logger.LogWarning(
                    "Physical file missing. StoredFileId={StoredFileId}, Path={PhysicalPath}",
                    storedFile.Id,
                    fullPath);

                return null;
            }

            var stream = new FileStream(
                fullPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 64 * 1024,
                useAsync: true);

            return new FileContentResult
            {
                File = Map(tenantFile, storedFile),
                Content = stream
            };
        }

        public async Task<bool> DeleteFileAsync(
            Guid fileGuid,
            CancellationToken cancellationToken = default)
        {
            _tenantAuthorizationService.DemandPermission(TenantPermission.Delete);

            var current = _currentTenantAccessor.GetRequired();
            var tenantId = GetRequiredTenantId(current);

            var tenantFile = await _tenantFileRepository.GetByTenantAndFileGuidAsync(
                tenantId,
                fileGuid,
                cancellationToken);

            if (tenantFile is null)
                return false;

            var softDeleted = await _tenantFileRepository.SoftDeleteAsync(
                tenantFile.Id,
                DateTime.UtcNow,
                cancellationToken);

            if (!softDeleted)
                return false;

            await _storedFileRepository.DecrementReferenceCountAsync(
                tenantFile.StoredFileId,
                cancellationToken);

            _logger.LogInformation(
                "Tenant file deleted. TenantFileId={TenantFileId}, TenantId={TenantId}, StoredFileId={StoredFileId}, FileGuid={FileGuid}",
                tenantFile.Id,
                tenantFile.TenantId,
                tenantFile.StoredFileId,
                tenantFile.FileGuid);

            await TryDeleteSingleOrphanFileAsync(
                tenantFile.StoredFileId,
                cancellationToken);

            return true;
        }

        public async Task<int> DeleteOrphanFilesAsync(
            CancellationToken cancellationToken = default)
        {
            var current = _currentTenantAccessor.GetRequired();

            if (!current.IsAdmin)
                throw new UnauthorizedAccessException("Administrative token is required.");

            var orphanFiles = await _storedFileRepository.GetOrphanFilesAsync(cancellationToken);
            var deletedCount = 0;

            foreach (var orphan in orphanFiles)
            {
                if (await TryDeleteSingleOrphanFileAsync(orphan.Id, cancellationToken))
                {
                    deletedCount++;
                }
            }

            _logger.LogInformation(
                "Orphan cleanup completed. DeletedCount={DeletedCount}",
                deletedCount);

            return deletedCount;
        }

        private async Task<bool> TryDeleteSingleOrphanFileAsync(
            Guid storedFileId,
            CancellationToken cancellationToken)
        {
            var storedFile = await _storedFileRepository.GetByIdAsync(
                storedFileId,
                cancellationToken);

            if (storedFile is null)
                return false;

            if (storedFile.IsDeleted)
                return false;

            if (storedFile.ReferenceCount > 0)
                return false;

            var fullPath = GetFullPhysicalPath(storedFile.PhysicalPath);

            if (File.Exists(fullPath))
            {
                TryDeleteFile(fullPath);
            }

            await _storedFileRepository.MarkDeletedAsync(
                storedFile.Id,
                DateTime.UtcNow,
                cancellationToken);

            await _storedFileRepository.DeleteAsync(
                storedFile.Id,
                cancellationToken);

            _logger.LogInformation(
                "Orphan physical file removed. StoredFileId={StoredFileId}, Path={PhysicalPath}",
                storedFile.Id,
                storedFile.PhysicalPath);

            return true;
        }

        private string GetFullPhysicalPath(string relativePath)
        {
            var normalized = relativePath
                .Replace('/', Path.DirectorySeparatorChar)
                .Replace('\\', Path.DirectorySeparatorChar);

            return Path.Combine(_options.DataPath, normalized);
        }

        private static string BuildStorageRelativePath(string sha256, string fileName)
        {
            var extension = Path.GetExtension(fileName);
            var level1 = sha256[..2];
            var level2 = sha256.Substring(2, 2);

            return Path.Combine(level1, level2, $"{sha256}{extension}");
        }

        private bool TryDeleteFile(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);

                return true;
            }
            catch
            {
                _logger.LogWarning("Failed to delete file at path: {Path}", path);
            }
            return false;
        }

        private bool TryMoveFile(string path, string destination)
        {
            try
            {
                if (File.Exists(path))
                    File.Move(path, destination);
                return true;
            }
            catch
            {
                _logger.LogWarning("Failed to move file from {Path} to {Destination}", path, destination);
            }
            return false;
        }

        private static StoredTenantFileDto Map(TenantFile tenantFile, StoredFile storedFile)
        {
            return new StoredTenantFileDto
            {
                TenantFileId = tenantFile.Id,
                FileGuid = tenantFile.FileGuid,
                TenantId = tenantFile.TenantId,
                StoredFileId = tenantFile.StoredFileId,
                FileName = tenantFile.FileName,
                Category = tenantFile.Category,
                ExternalKey = tenantFile.ExternalKey,
                ContentType = storedFile.ContentType,
                FileSize = storedFile.FileSize,
                Sha256 = storedFile.Sha256,
                Crc32 = storedFile.Crc32,
                CreatedUtc = tenantFile.CreatedUtc
            };
        }

        private static Guid GetRequiredTenantId(CurrentTenantContext current)
        {
            if (!current.TenantId.HasValue)
                throw new InvalidOperationException("Tenant context is required for this operation.");

            return current.TenantId.Value;
        }

        private static async Task<(string Sha256, string Crc32, long FileSize)> SaveToTempAndCalculateHashesAsync(
                Stream input,
                string tempPath,
                CancellationToken cancellationToken)
        {
            using var sha256 = SHA256.Create();
            long fileSize = 0;
            var buffer = new byte[64 * 1024];

            await using (var output = new FileStream(
                tempPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: buffer.Length,
                useAsync: true))
            {
                while (true)
                {
                    var read = await input.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
                    if (read == 0)
                        break;

                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);

                    sha256.TransformBlock(buffer, 0, read, null, 0);
                    fileSize += read;
                }

                await output.FlushAsync(cancellationToken);
            }

            sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            var sha256Hex = Convert.ToHexString(sha256.Hash!);

            byte[] fileBytes = await File.ReadAllBytesAsync(tempPath, cancellationToken);
            byte[] crcBytes = System.IO.Hashing.Crc32.Hash(fileBytes);
            var crc32Hex = Convert.ToHexString(crcBytes);

            return (sha256Hex, crc32Hex, fileSize);
        }
    }
}
