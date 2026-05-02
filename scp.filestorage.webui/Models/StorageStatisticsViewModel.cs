namespace scp.filestorage.webui.Models
{
    public sealed class StorageStatisticsViewModel
    {
        public long UsedBytes { get; set; }
        public int StoredFileCount { get; set; }
        public int TenantFileCount { get; set; }
        public int TenantCount { get; set; }
        public IReadOnlyList<LargestFileViewModel> LargestFiles { get; set; } = [];
        public IReadOnlyList<TenantStorageStatisticsViewModel> Tenants { get; set; } = [];
    }

    public sealed class LargestFileViewModel
    {
        public Guid FileGuid { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string? Category { get; set; }
        public string? ExternalKey { get; set; }
        public Guid TenantId { get; set; }
        public Guid TenantGuid { get; set; }
        public string TenantName { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public DateTime CreatedUtc { get; set; }
    }

    public sealed class TenantStorageStatisticsViewModel
    {
        public Guid TenantId { get; set; }
        public Guid TenantGuid { get; set; }
        public string TenantName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public int FileCount { get; set; }
        public long UsedBytes { get; set; }
    }
}
