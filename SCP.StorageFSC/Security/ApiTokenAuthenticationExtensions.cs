using scp.filestorage.Security;

namespace SCP.StorageFSC.Security
{
    public static class ApiTokenAuthenticationExtensions
    {
        public static IServiceCollection AddApiTokenAuthentication(this IServiceCollection services)
        {
            services
                .AddAuthentication("ApiKey")
                .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>("ApiKey", options =>
                {
                    options.HeaderName = "X-Api-Key";
                    options.SchemeName = "ApiKey";
                });

            services.AddAuthorization(options =>
            {
                options.AddPolicy("FilesRead", policy =>
                    policy.RequireAuthenticatedUser()
                          .RequireClaim("scope", "files.read"));

                options.AddPolicy("FilesWrite", policy =>
                    policy.RequireAuthenticatedUser()
                          .RequireClaim("scope", "files.write"));

                options.AddPolicy("TenantOnly", policy =>
                    policy.RequireAuthenticatedUser()
                          .RequireClaim("tenant_id"));
            });
            return services;
        }

        public static IApplicationBuilder UseApiTokenAuthentication(this IApplicationBuilder app)
        {
            return app.UseMiddleware<ApiTokenAuthenticationMiddleware>();
        }
    }
}
