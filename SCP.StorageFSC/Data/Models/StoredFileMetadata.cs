using SCP.StorageFSC.Data.Models;

namespace scp.filestorage.Data.Models
{
    public sealed class StoredFileMetadata : EntityBase
    {
        public Guid StoredFileId { get; set; }
        public string Key { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }
}