namespace SCP.StorageFSC.Data.Dto
{
    public sealed class StorageStatisticsDto
    {
        public long UsedBytes { get; set; }
        public int StoredFileCount { get; set; }
        public int TenantFileCount { get; set; }
        public int TenantCount { get; set; }
        public IReadOnlyList<LargestFileDto> LargestFiles { get; set; } = [];
        public IReadOnlyList<TenantStorageStatisticsDto> Tenants { get; set; } = [];
    }

    public sealed class LargestFileDto
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

    public sealed class TenantStorageStatisticsDto
    {
        public Guid TenantId { get; set; }
        public Guid TenantGuid { get; set; }
        public string TenantName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public int FileCount { get; set; }
        public long UsedBytes { get; set; }
    }
}
