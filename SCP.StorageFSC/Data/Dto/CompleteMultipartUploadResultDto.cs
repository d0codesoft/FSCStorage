using scp.filestorage.Data.Models;

namespace scp.filestorage.Data.Dto
{
    public sealed class CompleteMultipartUploadResultDto
    {
        public Guid UploadId { get; set; }
        public Guid TenantId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string? ContentType { get; set; }
        public string? FinalChecksumSha256 { get; set; }
        public string PhysicalPath { get; set; } = string.Empty;
        public string RelativePath { get; set; } = string.Empty;
        public DateTime CompletedAtUtc { get; set; }
        public MultipartUploadStatus Status { get; set; }
        public Guid? StoredFileId { get; set; }
    }
}