using SCP.StorageFSC.Data.Models;

namespace scp.filestorage.Data.Models
{
    public sealed class MultipartUploadPart : EntityBase
    {
        public Guid MultipartUploadSessionId { get; set; }
        public int PartNumber { get; set; }

        public long OffsetBytes { get; set; }
        public long SizeInBytes { get; set; }

        public string StorageKey { get; set; } = string.Empty;        // temp/{tenantId}/{uploadId}/part-000001
        public string? ChecksumSha256 { get; set; }
        public string? ProviderPartETag { get; set; }

        public MultipartUploadPartStatus Status { get; set; }         // Pending, Uploaded, Verified, Failed

        public DateTime? UploadedAtUtc { get; set; }
        public string? ErrorMessage { get; set; }
        public int RetryCount { get; set; }
        public DateTime? LastFailedAtUtc { get; set; }
    }
}