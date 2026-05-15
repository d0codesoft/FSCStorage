using Microsoft.Extensions.Options;
using SCP.StorageFSC.Data.Repositories;
using SCP.StorageFSC.InterfacesService;

namespace SCP.StorageFSC.Services
{
    public sealed class DeletedTenantCleanupService : IDeletedTenantCleanupService
    {
        private readonly IDeletedTenantRepository _deletedTenantRepository;
        private readonly IStoredFileRepository _storedFileRepository;
        private readonly ApplicationPaths _options;
        private readonly ILogger<DeletedTenantCleanupService> _logger;

        public DeletedTenantCleanupService(
            IDeletedTenantRepository deletedTenantRepository,
            IStoredFileRepository storedFileRepository,
            ApplicationPaths options,
            ILogger<DeletedTenantCleanupService> logger)
        {
            _deletedTenantRepository = deletedTenantRepository;
            _storedFileRepository = storedFileRepository;
            _options = options;
            _logger = logger;
        }

        public async Task<DeletedTenantCleanupResult> CleanupAsync(CancellationToken cancellationToken = default)
        {
            var pendingTenants = await _deletedTenantRepository.GetPendingCleanupAsync(cancellationToken);
            if (pendingTenants.Count == 0)
            {
                return new DeletedTenantCleanupResult();
            }

            var orphanFiles = await _storedFileRepository.GetOrphanFilesAsync(cancellationToken);
            var deletedFileCount = 0;

            foreach (var orphan in orphanFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (orphan.IsDeleted || orphan.ReferenceCount > 0)
                    continue;

                var fullPath = GetFullPhysicalPath(orphan.PhysicalPath);
                TryDeleteFile(fullPath);

                await _storedFileRepository.MarkDeletedAsync(orphan.Id, DateTime.UtcNow, cancellationToken);
                await _storedFileRepository.DeleteAsync(orphan.Id, cancellationToken);
                deletedFileCount++;
            }

            var completedUtc = DateTime.UtcNow;
            var cleanedTenantCount = await _deletedTenantRepository.MarkCleanupCompletedAsync(
                pendingTenants.Select(x => x.Id).ToArray(),
                completedUtc,
                cancellationToken);

            _logger.LogInformation(
                "Deleted tenant cleanup completed. PendingTenantCount={PendingTenantCount}, CleanedTenantCount={CleanedTenantCount}, DeletedFileCount={DeletedFileCount}",
                pendingTenants.Count,
                cleanedTenantCount,
                deletedFileCount);

            return new DeletedTenantCleanupResult
            {
                PendingTenantCount = pendingTenants.Count,
                CleanedTenantCount = cleanedTenantCount,
                DeletedFileCount = deletedFileCount
            };
        }

        private string GetFullPhysicalPath(string relativePath)
        {
            var normalized = relativePath
                .Replace('/', Path.DirectorySeparatorChar)
                .Replace('\\', Path.DirectorySeparatorChar);

            return Path.Combine(_options.DataPath, normalized);
        }

        private void TryDeleteFile(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete orphaned physical file at path {Path}", path);
            }
        }
    }
}
