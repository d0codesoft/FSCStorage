namespace SCP.StorageFSC.SecurityPermission
{
    public enum TenantPermission
    {
        None = 0,
        Read = 1,
        Write = 2,
        Delete = 3,
        Admin = 4
    }

    public enum TenantAccessMode
    {
        Authenticated = 0,
        AdminOnly = 1,
        AdminOrSameTenant = 2
    }
}
