using scp.filestorage.Data.Models;

namespace scp.filestorage.Data.Dto
{
    public sealed class UploadMultipartPartResultDto
    {
        public Guid UploadId { get; set; }
        public int PartNumber { get; set; }
        public long OffsetBytes { get; set; }
        public long SizeInBytes { get; set; }
        public string StorageKey { get; set; } = string.Empty;
        public string? ChecksumSha256 { get; set; }
        public MultipartUploadPartStatus Status { get; set; }
        public DateTime? UploadedAtUtc { get; set; }
    }
}
