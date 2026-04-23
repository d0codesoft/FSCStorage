namespace SCP.StorageFSC.Security
{
    public sealed class CurrentTenantContext
    {
        public Guid? TenantId { get; set; }
        public Guid? TenantGuid { get; set; }
        public string TenantName { get; set; } = string.Empty;

        public Guid TokenId { get; set; }
        public string TokenName { get; set; } = string.Empty;

        public bool IsAdmin { get; set; }
        public bool CanRead { get; set; }
        public bool CanWrite { get; set; }
        public bool CanDelete { get; set; }
    }
}
