namespace SCP.StorageFSC.SecurityPermission
{
    public sealed class TenantAccessOptions
    {
        public TenantAccessMode AccessMode { get; set; }
        public TenantPermission RequiredPermission { get; set; } = TenantPermission.None;
        public string? RouteParameterName { get; set; }
        public string? RouteParameterGuidName { get; set; }
    }
}
