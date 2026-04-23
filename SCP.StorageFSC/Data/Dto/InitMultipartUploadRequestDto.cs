namespace scp.filestorage.Data.Dto
{
    public sealed class InitMultipartUploadRequestDto
    {
        public string FileName { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string? ContentType { get; set; }
        public long PartSize { get; set; }
        public string? ExpectedChecksumSha256 { get; set; }
        public Guid TenantId { get; set; }
        public Dictionary<string, string>? Metadata { get; set; }
        public DateTime? ExpiresAtUtc { get; set; }
    }
}
