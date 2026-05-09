using scp.filestorage.Security;
using Microsoft.AspNetCore.Authentication.Cookies;
using SCP.StorageFSC.Data.Dto;

namespace SCP.StorageFSC.Security
{
    public static class ApiTokenAuthenticationExtensions
    {
        public const string ApiKeyScheme = "ApiKey";
        public const string CookieScheme = "FscCookie";
        public const string ApiTokenOnlyPolicy = "ApiTokenOnly";
        public const string WebUserOnlyPolicy = "WebUserOnly";
        public const string AdminOnlyPolicy = "AdminOnly";

        public static IServiceCollection AddApiTokenAuthentication(this IServiceCollection services)
        {
            services
                .AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = ApiKeyScheme;
                    options.DefaultChallengeScheme = ApiKeyScheme;
                })
                .AddCookie(CookieScheme, options =>
                {
                    options.Cookie.Name = "__Host-FscStorage";
                    options.Cookie.HttpOnly = true;
                    options.Cookie.SameSite = SameSiteMode.Strict;
                    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                    options.SlidingExpiration = true;
                    options.Events = new CookieAuthenticationEvents
                    {
                        OnRedirectToLogin = context => WriteCookieChallengeAsync(context.HttpContext),
                        OnRedirectToAccessDenied = context => WriteCookieForbiddenAsync(context.HttpContext)
                    };
                })
                .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(ApiKeyScheme, options =>
                {
                    options.HeaderName = "X-Api-Key";
                    options.SchemeName = ApiKeyScheme;
                });

            services.AddAuthorization(options =>
            {
                options.AddPolicy(ApiTokenOnlyPolicy, policy =>
                    policy.AddAuthenticationSchemes(ApiKeyScheme)
                          .RequireAuthenticatedUser()
                          .RequireClaim("auth_type", AuthType.ApiToken));

                options.AddPolicy(WebUserOnlyPolicy, policy =>
                    policy.AddAuthenticationSchemes(CookieScheme)
                          .RequireAuthenticatedUser()
                          .RequireClaim("auth_type", AuthType.WebApp));

                options.AddPolicy(AdminOnlyPolicy, policy =>
                    policy.AddAuthenticationSchemes(CookieScheme)
                          .RequireAuthenticatedUser()
                          .RequireClaim("auth_type", AuthType.WebApp)
                          .RequireRole("Admin"));

                options.AddPolicy("FilesRead", policy =>
                    policy.AddAuthenticationSchemes(ApiKeyScheme)
                          .RequireAuthenticatedUser()
                          .RequireClaim("scope", "files.read"));

                options.AddPolicy("FilesWrite", policy =>
                    policy.AddAuthenticationSchemes(ApiKeyScheme)
                          .RequireAuthenticatedUser()
                          .RequireClaim("scope", "files.write"));

                options.AddPolicy("TenantOnly", policy =>
                    policy.AddAuthenticationSchemes(ApiKeyScheme)
                          .RequireAuthenticatedUser()
                          .RequireClaim("tenant_id"));
            });
            return services;
        }

        public static IApplicationBuilder UseApiTokenAuthentication(this IApplicationBuilder app)
        {
            return app.UseMiddleware<ApiTokenAuthenticationMiddleware>();
        }

        private static Task WriteCookieChallengeAsync(HttpContext context)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return context.Response.WriteAsJsonAsync(ApiErrorResponse.Create(
                context,
                "Unauthorized",
                "Cookie authentication is required."));
        }

        private static Task WriteCookieForbiddenAsync(HttpContext context)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return context.Response.WriteAsJsonAsync(ApiErrorResponse.Create(
                context,
                "AccessDenied",
                "The current principal is not allowed to access this resource."));
        }
    }
}
