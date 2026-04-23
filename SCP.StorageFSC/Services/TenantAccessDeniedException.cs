namespace SCP.StorageFSC.Services
{
    public sealed class TenantAccessDeniedException : Exception
    {
        public TenantAccessDeniedException(string message)
            : base(message)
        {
        }
    }
}
