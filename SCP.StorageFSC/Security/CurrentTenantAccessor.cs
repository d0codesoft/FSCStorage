namespace SCP.StorageFSC.Security
{
    public sealed class CurrentTenantAccessor : ICurrentTenantAccessor
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CurrentTenantAccessor(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public CurrentTenantContext? Current =>
            _httpContextAccessor.HttpContext?.GetCurrentTenant();

        public CurrentTenantContext GetRequired() =>
            Current ?? throw new InvalidOperationException("Current tenant is not available.");
    }
}
