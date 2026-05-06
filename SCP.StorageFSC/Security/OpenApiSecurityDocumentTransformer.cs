using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace SCP.StorageFSC.Security
{
    public sealed class OpenApiSecurityDocumentTransformer : IOpenApiDocumentTransformer
    {
        public Task TransformAsync(
            OpenApiDocument document,
            OpenApiDocumentTransformerContext context,
            CancellationToken cancellationToken)
        {
            var components = document.Components ??= new OpenApiComponents();
            var securitySchemes = components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();

            securitySchemes["ApiToken"] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.ApiKey,
                Name = "X-Api-Key",
                In = ParameterLocation.Header,
                Description = "Tenant-bound API token for external machine API endpoints."
            };

            securitySchemes["CookieAuth"] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.ApiKey,
                Name = "__Host-FscStorage",
                In = ParameterLocation.Cookie,
                Description = "HTTP-only cookie issued by /auth/login for Web UI API endpoints."
            };

            return Task.CompletedTask;
        }
    }
}
