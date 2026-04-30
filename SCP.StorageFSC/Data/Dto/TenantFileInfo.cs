namespace SCP.StorageFSC.Data.Dto
{
    using SCP.StorageFSC.Data.Models;

    public sealed class TenantFileInfo
    {
        public Guid TenantGuid { get; set; }
        public Guid FileGuid { get; set; }
        public string FileName { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string Sha256 { get; set; } = string.Empty;
        public string Crc32 { get; set; } = string.Empty;
        public string? ContentType { get; set; }
        public FilestoreStateCompress FilestoreStateCompress { get; set; }
        public DateTime CreatedUtc { get; set; }
    }
}
