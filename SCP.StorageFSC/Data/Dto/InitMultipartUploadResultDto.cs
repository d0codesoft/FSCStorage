using scp.filestorage.Data.Models;

namespace scp.filestorage.Data.Dto
{
    public sealed class InitMultipartUploadResultDto
    {
        public Guid UploadId { get; set; }
        public Guid TenantId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public long PartSize { get; set; }
        public int TotalParts { get; set; }
        public MultipartUploadStatus Status { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime? ExpiresAtUtc { get; set; }
    }
}
