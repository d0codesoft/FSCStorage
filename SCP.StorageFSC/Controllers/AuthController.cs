using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using scp.filestorage.InterfacesService;
using SCP.StorageFSC.Data.Dto;
using SCP.StorageFSC.Security;

namespace SCP.StorageFSC.Controllers
{
    [ApiController]
    [Route("auth")]
    public sealed class AuthController : ControllerBase
    {
        private readonly IApiTokenService _apiTokenService;

        public AuthController(IApiTokenService apiTokenService)
        {
            _apiTokenService = apiTokenService;
        }

        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login(
            [FromBody] LoginRequest request,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.ApiToken))
                return BadRequest(ApiErrorResponse.Create(HttpContext, "ValidationError", "API token is required."));

            var result = await _apiTokenService.ValidateAsync(request.ApiToken.Trim(), cancellationToken);
            if (result is null || !result.Success)
                return Unauthorized(ApiErrorResponse.Create(HttpContext, "InvalidApiToken", "API token is missing or invalid."));

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, result.TokenId.ToString()),
                new(ClaimTypes.Name, result.Name),
                new("auth_type", "web_user")
            };

            if (result.TenantId.HasValue)
                claims.Add(new Claim("tenant_id", result.TenantId.Value.ToString()));

            foreach (var scope in result.Scopes)
                claims.Add(new Claim("scope", scope));

            if (result.IsAdmin || result.Scopes.Any(scope => string.Equals(scope, "admin", StringComparison.OrdinalIgnoreCase)))
                claims.Add(new Claim(ClaimTypes.Role, "Admin"));

            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, ApiTokenAuthenticationExtensions.CookieScheme));
            await HttpContext.SignInAsync(
                ApiTokenAuthenticationExtensions.CookieScheme,
                principal,
                new AuthenticationProperties
                {
                    IsPersistent = request.Remember,
                    IssuedUtc = DateTimeOffset.UtcNow
                });

            return Ok(ToMeResponse(principal));
        }

        [HttpPost("logout")]
        [Authorize(Policy = ApiTokenAuthenticationExtensions.WebUserOnlyPolicy)]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(ApiTokenAuthenticationExtensions.CookieScheme);
            return NoContent();
        }

        [HttpGet("me")]
        [Authorize(Policy = ApiTokenAuthenticationExtensions.WebUserOnlyPolicy)]
        public IActionResult Me()
        {
            return Ok(ToMeResponse(User));
        }

        private static MeResponse ToMeResponse(ClaimsPrincipal principal)
        {
            return new MeResponse
            {
                Name = principal.FindFirstValue(ClaimTypes.Name) ?? string.Empty,
                IsAdmin = principal.IsInRole("Admin"),
                TenantId = Guid.TryParse(principal.FindFirstValue("tenant_id"), out var tenantId)
                    ? tenantId
                    : null,
                Scopes = principal.FindAll("scope").Select(claim => claim.Value).ToArray()
            };
        }
    }

    public sealed class LoginRequest
    {
        public string ApiToken { get; set; } = string.Empty;
        public bool Remember { get; set; }
    }

    public sealed class MeResponse
    {
        public string Name { get; set; } = string.Empty;
        public bool IsAdmin { get; set; }
        public Guid? TenantId { get; set; }
        public string[] Scopes { get; set; } = [];
    }
}
