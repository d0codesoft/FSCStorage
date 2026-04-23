namespace SCP.StorageFSC.Security
{
    public static class HttpContextTenantExtensions
    {
        public static CurrentTenantContext? GetCurrentTenant(this HttpContext httpContext)
        {
            if (httpContext.Items.TryGetValue(
                    TenantContextConstants.CurrentTenantContextItemName,
                    out var value) &&
                value is CurrentTenantContext tenantContext)
            {
                return tenantContext;
            }

            return null;
        }

        public static CurrentTenantContext GetRequiredCurrentTenant(this HttpContext httpContext)
        {
            return httpContext.GetCurrentTenant()
                   ?? throw new InvalidOperationException("Current tenant context is not available.");
        }
    }
}
