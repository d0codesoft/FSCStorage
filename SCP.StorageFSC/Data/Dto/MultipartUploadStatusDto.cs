using scp.filestorage.Data.Models;

namespace scp.filestorage.Data.Dto
{
    public sealed class MultipartUploadStatusDto
    {
        public Guid UploadId { get; set; }
        public Guid TenantId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string NormalizedFileName { get; set; } = string.Empty;
        public string Extension { get; set; } = string.Empty;
        public string? ContentType { get; set; }
        public long TotalFileSize { get; set; }
        public long PartSize { get; set; }
        public int TotalParts { get; set; }
        public int UploadedPartCount { get; set; }
        public IReadOnlyList<int> UploadedParts { get; set; } = Array.Empty<int>();
        public MultipartUploadStatus Status { get; set; }
        public string? ErrorCode { get; set; }
        public string? ErrorMessage { get; set; }
        public string TempStoragePrefix { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; }
        public DateTime? UpdatedAtUtc { get; set; }
        public DateTime? CompletedAtUtc { get; set; }
        public DateTime? ExpiresAtUtc { get; set; }
        public Guid? StoredFileId { get; set; }
    }
}
