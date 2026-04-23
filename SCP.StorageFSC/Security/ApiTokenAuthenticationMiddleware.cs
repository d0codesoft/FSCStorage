using SCP.StorageFSC.Data.Repositories;
using Serilog.Context;
using System.Security.Claims;

namespace SCP.StorageFSC.Security
{
    public sealed class ApiTokenAuthenticationMiddleware
    {
        private const string TenantIdHeaderName = "X-Tenant-Id";

        private readonly RequestDelegate _next;
        private readonly ILogger<ApiTokenAuthenticationMiddleware> _logger;

        public ApiTokenAuthenticationMiddleware(
            RequestDelegate next,
            ILogger<ApiTokenAuthenticationMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(
            HttpContext context,
            ITenantRepository tenantRepository)
        {
            if (ShouldSkip(context) || !(context.User.Identity?.IsAuthenticated ?? false))
            {
                await _next(context);
                return;
            }

            var currentTenant = await CreateCurrentTenantContextAsync(context, tenantRepository);
            if (currentTenant is null)
            {
                await _next(context);
                return;
            }

            context.Items[TenantContextConstants.CurrentTenantContextItemName] = currentTenant;

            using (LogContext.PushProperty("TenantGuid", currentTenant.TenantGuid))
            using (LogContext.PushProperty("TenantId", currentTenant.TenantId))
            using (LogContext.PushProperty("TenantName", currentTenant.TenantName))
            using (LogContext.PushProperty("TokenId", currentTenant.TokenId))
            using (LogContext.PushProperty("IsAdmin", currentTenant.IsAdmin))
            {
                await _next(context);
            }
        }

        private static bool ShouldSkip(HttpContext context)
        {
            var path = context.Request.Path.Value ?? string.Empty;

            return path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase)
                   || path.StartsWith("/health", StringComparison.OrdinalIgnoreCase)
                   || path.StartsWith("/openapi", StringComparison.OrdinalIgnoreCase);
        }

        private async Task<CurrentTenantContext?> CreateCurrentTenantContextAsync(
            HttpContext context,
            ITenantRepository tenantRepository)
        {
            var user = context.User;
            var authType = user.FindFirst("auth_type")?.Value;

            if (!string.Equals(authType, "api_token", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (!Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var tokenId))
            {
                _logger.LogWarning("Authenticated API token request does not contain a valid token id claim.");
                return null;
            }

            var isAdmin = HasScope(user, "admin") || user.IsInRole("Admin");
            var currentTenant = new CurrentTenantContext
            {
                TokenId = tokenId,
                TokenName = user.FindFirstValue(ClaimTypes.Name) ?? string.Empty,
                IsAdmin = isAdmin,
                CanRead = isAdmin || HasScope(user, "read") || HasScope(user, "files.read"),
                CanWrite = isAdmin || HasScope(user, "write") || HasScope(user, "files.write"),
                CanDelete = isAdmin || HasScope(user, "delete") || HasScope(user, "files.delete")
            };

            var tenant = await ResolveTenantAsync(context, tenantRepository, isAdmin);

            if (tenant is not null)
            {
                currentTenant.TenantId = tenant.Id;
                currentTenant.TenantGuid = tenant.TenantGuid;
                currentTenant.TenantName = tenant.Name;
            }

            return currentTenant;
        }

        private async Task<Data.Models.Tenant?> ResolveTenantAsync(
            HttpContext context,
            ITenantRepository tenantRepository,
            bool isAdmin)
        {
            var tenantIdRaw = context.Request.Headers[TenantIdHeaderName].FirstOrDefault();

            if (isAdmin)
            {
                if (string.IsNullOrWhiteSpace(tenantIdRaw))
                    return null;

                if (!Guid.TryParse(tenantIdRaw, out var tenantGuid))
                {
                    _logger.LogWarning("Tenant logging enrichment skipped because X-Tenant-Id is invalid.");
                    return null;
                }

                var tenant = await tenantRepository.GetByGuidAsync(tenantGuid);
                if (tenant is null)
                {
                    _logger.LogWarning("Tenant logging enrichment skipped because tenant {TenantGuid} was not found.", tenantGuid);
                    return null;
                }

                if (!tenant.IsActive)
                {
                    _logger.LogWarning("Tenant logging enrichment skipped because tenant {TenantId} is inactive.", tenant.Id);
                    return null;
                }

                return tenant;
            }

            var tenantIdClaim = context.User.FindFirst("tenant_id")?.Value;
            if (!Guid.TryParse(tenantIdClaim, out var tenantId))
            {
                _logger.LogWarning("Authenticated tenant token request does not contain a valid tenant_id claim.");
                return null;
            }

            var currentTenant = await tenantRepository.GetByIdAsync(tenantId);
            if (currentTenant is null)
            {
                _logger.LogWarning("Tenant logging enrichment skipped because tenant {TenantId} was not found.", tenantId);
                return null;
            }

            return currentTenant;
        }

        private static bool HasScope(ClaimsPrincipal user, string scope)
        {
            return user.Claims.Any(x =>
                string.Equals(x.Type, "scope", StringComparison.OrdinalIgnoreCase)
                && string.Equals(x.Value, scope, StringComparison.OrdinalIgnoreCase));
        }
    }
}
