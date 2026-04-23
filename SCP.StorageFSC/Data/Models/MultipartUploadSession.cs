using SCP.StorageFSC.Data.Models;

namespace scp.filestorage.Data.Models
{
    public sealed class MultipartUploadSession : EntityBase
    {
        public Guid UploadId { get; set; }
        public Guid TenantId { get; set; }

        public string OriginalFileName { get; set; } = string.Empty;
        public string NormalizedFileName { get; set; } = string.Empty;
        public string Extension { get; set; } = string.Empty;
        public string? ContentType { get; set; }

        public long TotalFileSize { get; set; }
        public long PartSize { get; set; }
        public int TotalParts { get; set; }

        public string? ExpectedChecksumSha256 { get; set; }
        public string? FinalChecksumSha256 { get; set; }

        public MultipartUploadStatus Status { get; set; } // Created, Uploading, Completing, Completed, Aborted, Failed, Expired
        public string? ErrorCode { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime? FailedAtUtc { get; set; }

        public string StorageProvider { get; set; } = string.Empty;   // Disk, S3, Minio
        public string? TempStorageBucket { get; set; }
        public string TempStoragePrefix { get; set; } = string.Empty; // temp/{tenantId}/{uploadId}/

        public DateTime? CompletedAtUtc { get; set; }
        public DateTime? ExpiresAtUtc { get; set; }

        public Guid? StoredFileId { get; set; }
    }
}