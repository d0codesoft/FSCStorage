using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;

namespace scp.filestorage.webui.Services
{
    public sealed class CurrentUserContext
    {
        public Guid UserId { get; set; }
        public string? Name { get; set; }
        public bool IsAuthenticated { get; set; }
        public bool IsAdmin { get; set; }
        public Guid? TenantId { get; set; }
    }

    public sealed class CurrentUserService
    {
        private readonly AuthenticationStateProvider _authenticationStateProvider;
        private readonly ILogger<CurrentUserService> _logger;

        public CurrentUserService(
            AuthenticationStateProvider authenticationStateProvider,
            ILogger<CurrentUserService> logger)
        {
            _authenticationStateProvider = authenticationStateProvider;
            _logger = logger;
        }

        private Guid GetUserId(ClaimsPrincipal user)
        {
#if DEBUG
            foreach (var claim in user.Claims)
            {
                _logger.LogDebug(
                    "ClaimsPrincipal claim: Type={ClaimType}; Value={ClaimValue}; ValueType={ClaimValueType}; Issuer={ClaimIssuer}",
                    claim.Type,
                    claim.Value,
                    claim.ValueType,
                    claim.Issuer);
            }
#endif

            var value = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(value, out var userId) ? userId : Guid.Empty;
        }

        public async Task<CurrentUserContext> GetAsync()
        {
            var state = await _authenticationStateProvider.GetAuthenticationStateAsync();
            var user = state.User;

            return new CurrentUserContext
            {
                IsAuthenticated = user.Identity?.IsAuthenticated == true,
                Name = user.Identity?.Name,
                IsAdmin = user.IsInRole("Admin"),
                UserId = GetUserId(user),
                TenantId = Guid.TryParse(user.FindFirst("tenant_id")?.Value, out var tenantId)
                    ? tenantId
                    : null
            };
        }
    }
}
