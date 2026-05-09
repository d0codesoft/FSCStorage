using Microsoft.AspNetCore.Identity;
using scp.filestorage.Common;
using scp.filestorage.Security;
using SCP.StorageFSC.Data.Models;
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
            ITenantRepository tenantRepository,
            IApiTokenRepository apiTokenRepository)
        {
            if (ShouldSkip(context))
            {
                await _next(context);
                return;
            }

            var authType = context.User?.FindFirst("auth_type")?.Value ?? "Undefined";
            using var userLogContext = PushUserLogContext(context.User);

            var currentTenant = await CreateCurrentTenantContextAsync(
                context,
                tenantRepository,
                apiTokenRepository);

            if (currentTenant is null)
            {
                await _next(context);
                return;
            }

            context.Items[TenantContextConstants.CurrentTenantContextItemName] = currentTenant;

            using var tenantGuidLogContext = LogContext.PushProperty("TenantGuid", currentTenant.TenantGuid);
            using var tenantIdLogContext = LogContext.PushProperty("TenantId", currentTenant.TenantId);
            using var tenantNameLogContext = LogContext.PushProperty("TenantName", currentTenant.TenantName);
            using var tokenIdLogContext = LogContext.PushProperty("TokenId", currentTenant.TokenId);
            using var isAdminLogContext = LogContext.PushProperty("IsAdmin", currentTenant.IsAdmin);
            using var authTypeLogContext = LogContext.PushProperty("AuthType", authType);

            await _next(context);
        }

        /// <summary>
        /// Creates a log context for the authenticated user, including user ID, admin status, and authentication type.
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        private static IDisposable PushUserLogContext(ClaimsPrincipal? user)
        {
            if (user?.Identity?.IsAuthenticated != true)
                return EmptyDisposable.Instance;

            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            var isAdmin = user.IsInRole("Admin") || HasScope(user, "admin");
            var authType = user.FindFirst("auth_type")?.Value;

            // If user is authenticated but auth_type claim is missing or not "web_user",
            // we won't push user context to logs to avoid confusion, since we only want to enrich logs with web user info, not API token info.
            if (authType == null || authType != AuthType.WebApp)
            {
                return EmptyDisposable.Instance;
            }

            return new CompositeDisposable(
                LogContext.PushProperty("UserId", userId),
                LogContext.PushProperty("AuthType", authType));
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
            ITenantRepository tenantRepository,
            IApiTokenRepository apiTokenRepository)
        {
            var user = context.User;
            var authType = user.FindFirst("auth_type")?.Value;

            // Every authenticated request should have an auth_type claim indicating how the user was authenticated
            // (e.g. "web_user" or AuthType.ApiToken). If it's missing or not AuthType.ApiToken, we won't create a tenant context.
            if (!string.Equals(authType, AuthType.ApiToken, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (!Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var tokenId))
            {
                _logger.LogWarning("Authenticated API token request does not contain a valid token id claim.");
                return null;
            }

            var token = await apiTokenRepository.GetByIdAsync(tokenId);
            if (token is null)
            {
                _logger.LogWarning("API token with id {TokenId} was not found.", tokenId);
                return null;
            }
            var isAdmin = token.IsAdmin;
            var currentTenant = await ResolveTenantAsync(context, tenantRepository, isAdmin, token.TenantId);

            if (currentTenant is null)
                return null;

            var contextTenant = new CurrentTenantContext
            {
                TokenId = tokenId,
                TokenName = token.Name,
                IsAdmin = isAdmin,
                CanRead = isAdmin || HasScope(user, "read") || HasScope(user, "files.read"),
                CanWrite = isAdmin || HasScope(user, "write") || HasScope(user, "files.write"),
                CanDelete = isAdmin || HasScope(user, "delete") || HasScope(user, "files.delete")
            };

            return contextTenant;
        }

        private async Task<Data.Models.Tenant?> ResolveTenantAsync(
            HttpContext context,
            ITenantRepository tenantRepository,
            bool isAdmin,
            Guid? fallbackTenantId)
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
                tenantId = fallbackTenantId.GetValueOrDefault();

            if (tenantId == Guid.Empty)
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
