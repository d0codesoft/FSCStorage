namespace scp.filestorage.Data.Dto
{
    public sealed class UploadMultipartPartRequestDto
    {
        public Guid UploadId { get; set; }
        public int PartNumber { get; set; }
        public Stream Content { get; set; } = Stream.Null;
        public long ContentLength { get; set; }
        public string? PartChecksumSha256 { get; set; }
        public string? ContentType { get; set; }
    }
}
