namespace SCP.StorageFSC.Data.Models
{
    public sealed class DeletedTenant : EntityBase
    {
        public Guid TenantId { get; set; }
        public Guid UserId { get; set; }
        public Guid TenantGuid { get; set; }
        public string TenantName { get; set; } = string.Empty;
        public DateTime DeletedUtc { get; set; }
        public DateTime? CleanupCompletedUtc { get; set; }
    }
}
