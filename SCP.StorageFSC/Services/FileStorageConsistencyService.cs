using SCP.StorageFSC;
using SCP.StorageFSC.Data.Models;
using SCP.StorageFSC.Data.Repositories;
using System.Security.Cryptography;

namespace scp.filestorage.Services
{
    public sealed class FileStorageConsistencyService : IFileStorageConsistencyService
    {
        private readonly IStoredFileRepository _storedFileRepository;
        private readonly ITenantFileRepository _tenantFileRepository;
        private readonly ApplicationPaths _applicationPaths;
        private readonly ILogger<FileStorageConsistencyService> _logger;

        public FileStorageConsistencyService(
            IStoredFileRepository storedFileRepository,
            ITenantFileRepository tenantFileRepository,
            ApplicationPaths applicationPaths,
            ILogger<FileStorageConsistencyService> logger)
        {
            _storedFileRepository = storedFileRepository;
            _tenantFileRepository = tenantFileRepository;
            _applicationPaths = applicationPaths;
            _logger = logger;
        }

        public async Task<FileStorageConsistencyCheckResult> CheckAsync(
            CancellationToken cancellationToken = default)
        {
            var checkedAtUtc = DateTime.UtcNow;
            var storedFiles = await _storedFileRepository.GetActiveAsync(cancellationToken);
            var issues = new List<FileStorageConsistencyIssue>();

            foreach (var storedFile in storedFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var physicalPath = GetFullPhysicalPath(storedFile.PhysicalPath);
                if (!File.Exists(physicalPath))
                {
                    AddIssue(
                        issues,
                        storedFile,
                        "file.missing",
                        $"Physical file is missing. StoredFileId={storedFile.Id}",
                        physicalPath);
                    continue;
                }

                var fileInfo = new FileInfo(physicalPath);
                if (fileInfo.Length != storedFile.FileSize)
                {
                    AddIssue(
                        issues,
                        storedFile,
                        "file.size_mismatch",
                        $"File size mismatch. Expected={storedFile.FileSize}, Actual={fileInfo.Length}",
                        physicalPath);
                }

                var hashes = await CalculateHashesAsync(physicalPath, cancellationToken);
                if (!string.Equals(hashes.Sha256, storedFile.Sha256, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(hashes.Crc32, storedFile.Crc32, StringComparison.OrdinalIgnoreCase))
                {
                    AddIssue(
                        issues,
                        storedFile,
                        "file.hash_mismatch",
                        "File hash mismatch.",
                        physicalPath);
                }

                var tenantFiles = await _tenantFileRepository.GetByStoredFileIdAsync(
                    storedFile.Id,
                    cancellationToken);
                var activeReferenceCount = tenantFiles.Count(file => file.IsActive);

                if (activeReferenceCount == 0)
                {
                    AddIssue(
                        issues,
                        storedFile,
                        "db.orphan_stored_file",
                        "Stored file has no active tenant references.",
                        physicalPath);
                }

                if (storedFile.ReferenceCount != activeReferenceCount)
                {
                    AddIssue(
                        issues,
                        storedFile,
                        "db.reference_count_mismatch",
                        $"Reference count mismatch. Stored={storedFile.ReferenceCount}, ActiveTenantFiles={activeReferenceCount}",
                        physicalPath);
                }
            }

            var result = new FileStorageConsistencyCheckResult(
                checkedAtUtc,
                storedFiles.Count,
                CountIssues(issues, "file.missing"),
                CountIssues(issues, "file.size_mismatch"),
                CountIssues(issues, "file.hash_mismatch"),
                CountIssues(issues, "db.reference_count_mismatch"),
                CountIssues(issues, "db.orphan_stored_file"),
                issues);

            if (result.IsConsistent)
            {
                _logger.LogInformation(
                    "File storage consistency check completed successfully. CheckedFiles={CheckedFiles}",
                    result.CheckedFiles);
            }
            else
            {
                _logger.LogWarning(
                    "File storage consistency check completed with issues. CheckedFiles={CheckedFiles}, Issues={IssueCount}, MissingFiles={MissingFiles}, SizeMismatches={SizeMismatches}, HashMismatches={HashMismatches}, ReferenceCountMismatches={ReferenceCountMismatches}, OrphanFiles={OrphanFiles}",
                    result.CheckedFiles,
                    result.Issues.Count,
                    result.MissingFiles,
                    result.SizeMismatches,
                    result.HashMismatches,
                    result.ReferenceCountMismatches,
                    result.OrphanFiles);
            }

            return result;
        }

        private static void AddIssue(
            List<FileStorageConsistencyIssue> issues,
            StoredFile storedFile,
            string code,
            string message,
            string? physicalPath)
        {
            issues.Add(new FileStorageConsistencyIssue(
                storedFile.Id,
                code,
                message,
                physicalPath));
        }

        private string GetFullPhysicalPath(string relativePath)
        {
            var normalized = relativePath
                .Replace('/', Path.DirectorySeparatorChar)
                .Replace('\\', Path.DirectorySeparatorChar);

            return Path.Combine(_applicationPaths.DataPath, normalized);
        }

        private static int CountIssues(
            IReadOnlyList<FileStorageConsistencyIssue> issues,
            string code)
        {
            return issues.Count(issue => string.Equals(issue.Code, code, StringComparison.Ordinal));
        }

        private static async Task<(string Sha256, string Crc32)> CalculateHashesAsync(
            string filePath,
            CancellationToken cancellationToken)
        {
            using var sha256 = SHA256.Create();
            var crc32 = new System.IO.Hashing.Crc32();
            var buffer = new byte[1024 * 1024];

            await using var input = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: buffer.Length,
                useAsync: true);

            while (true)
            {
                var read = await input.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
                if (read == 0)
                    break;

                var chunk = buffer.AsMemory(0, read);
                sha256.TransformBlock(buffer, 0, read, null, 0);
                crc32.Append(chunk.Span);
            }

            sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);

            return (
                Convert.ToHexString(sha256.Hash!),
                Convert.ToHexString(crc32.GetCurrentHash()));
        }
    }
}
