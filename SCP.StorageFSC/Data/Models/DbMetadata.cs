namespace SCP.StorageFSC.Data.Models
{
    public sealed class DbMetadata : EntityBase
    {
        public int SchemaVersion { get; set; }
        public string SchemaName { get; set; } = string.Empty;
    }
}
