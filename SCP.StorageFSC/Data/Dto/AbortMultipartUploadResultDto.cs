using scp.filestorage.Data.Models;

namespace scp.filestorage.Data.Dto
{
    public sealed class AbortMultipartUploadResultDto
    {
        public Guid UploadId { get; set; }
        public MultipartUploadStatus Status { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
    }
}