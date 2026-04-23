namespace SCP.StorageFSC.Security
{
    public interface ICurrentTenantAccessor
    {
        CurrentTenantContext? Current { get; }
        CurrentTenantContext GetRequired();
    }
}
