namespace SCP.StorageFSC.Data.Dto
{
    using SCP.StorageFSC.Data.Models;

    

    public sealed class SaveFileRequest
    {
        public string FileName { get; set; } = string.Empty;
        public string? ContentType { get; set; }
        public string? Category { get; set; }
        public string? ExternalKey { get; set; }
        public Stream Content { get; set; } = Stream.Null;
    }

    public sealed class StoredTenantFileDto
    {
        public Guid TenantFileId { get; set; }
        public Guid FileGuid { get; set; }
        public Guid TenantId { get; set; }
        public Guid StoredFileId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string? Category { get; set; }
        public string? ExternalKey { get; set; }
        public string? ContentType { get; set; }
        public FilestoreStateCompress FilestoreStateCompress { get; set; }
        public long FileSize { get; set; }
        public string Sha256 { get; set; } = string.Empty;
        public string Crc32 { get; set; } = string.Empty;
        public DateTime CreatedUtc { get; set; }
    }

    public sealed class FileContentResult : IAsyncDisposable
    {
        public StoredTenantFileDto File { get; set; } = new();
        public Stream Content { get; set; } = Stream.Null;

        public async ValueTask DisposeAsync()
        {
            await Content.DisposeAsync();
        }
    }
}
