using Serilog;

namespace SCP.StorageFSC.Security
{
    public static class RequestLoggingExtensions
    {
        public static IApplicationBuilder UseApplicationRequestLogging(this IApplicationBuilder app)
        {
            return app.UseSerilogRequestLogging(options =>
            {
                options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
                {
                    var tenant = httpContext.GetCurrentTenant();

                    if (tenant is not null)
                    {
                        diagnosticContext.Set("TenantGuid", tenant.TenantGuid);
                        diagnosticContext.Set("TenantId", tenant.TenantId);
                        diagnosticContext.Set("TenantName", tenant.TenantName);
                        diagnosticContext.Set("TokenId", tenant.TokenId);
                        diagnosticContext.Set("IsAdmin", tenant.IsAdmin);
                    }

                    diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
                    diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
                };
            });
        }
    }
}
