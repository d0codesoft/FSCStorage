namespace scp.filestorage.Services
{
    public enum FileStorageBackgroundTaskType
    {
        MergeMultipartUpload = 0,
        CheckDatabaseConsistency = 1,
        CleanupDeletedTenantFiles = 2,
        UpdateSizeTenantStorageUsage = 3
    }

    public sealed record FileStorageBackgroundTask(
        Guid TaskId,
        FileStorageBackgroundTaskType Type,
        Guid? TenantId,
        Guid ValueId,
        DateTime CreatedAtUtc)
    {
        public string Descr => Type switch
        {
            FileStorageBackgroundTaskType.MergeMultipartUpload => BuildMergeMultipartUploadDescr(ValueId),
            FileStorageBackgroundTaskType.CheckDatabaseConsistency => "Check file storage database consistency",
            FileStorageBackgroundTaskType.CleanupDeletedTenantFiles => "Cleanup deleted tenant files",
            FileStorageBackgroundTaskType.UpdateSizeTenantStorageUsage => BuildUpdateTenantStorageUsageDescr(TenantId),
            _ => $"Background task {Type}"
        };

        public static FileStorageBackgroundTask MergeMultipartUpload(Guid tenantId, Guid uploadId) =>
            new(Guid.CreateVersion7(), FileStorageBackgroundTaskType.MergeMultipartUpload, tenantId, uploadId, DateTime.UtcNow);

        public static FileStorageBackgroundTask CheckDatabaseConsistency() =>
            new(Guid.CreateVersion7(), FileStorageBackgroundTaskType.CheckDatabaseConsistency, null, Guid.Empty, DateTime.UtcNow);

        public static FileStorageBackgroundTask CleanupDeletedTenantFiles() =>
            new(Guid.CreateVersion7(), FileStorageBackgroundTaskType.CleanupDeletedTenantFiles, null, Guid.Empty, DateTime.UtcNow);

        public static FileStorageBackgroundTask UpdateSizeTenantStorageUsage(Guid? tenantId = null) =>
            new(Guid.CreateVersion7(), FileStorageBackgroundTaskType.UpdateSizeTenantStorageUsage, tenantId, Guid.Empty, DateTime.UtcNow);

        private static string BuildMergeMultipartUploadDescr(Guid uploadId)
        {
            return uploadId == Guid.Empty
                ? "Merge multipart upload"
                : $"Merge multipart upload {uploadId}";
        }

        private static string BuildUpdateTenantStorageUsageDescr(Guid? tenantId)
        {
            return tenantId.HasValue
                ? $"Update tenant storage usage for {tenantId.Value}"
                : "Update tenant storage usage";
        }
    }
}
