using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using scp.filestorage.InterfacesService;
using SCP.StorageFSC.InterfacesService;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace scp.filestorage.Security
{
    public sealed class ApiKeyAuthenticationHandler
    : AuthenticationHandler<ApiKeyAuthenticationOptions>
    {
        private readonly IApiTokenService _apiTokenService;
        private readonly IApiAuthenticationAuditService _apiAuthenticationAuditService;

        public ApiKeyAuthenticationHandler(
            IOptionsMonitor<ApiKeyAuthenticationOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            IApiTokenService apiTokenService,
            IApiAuthenticationAuditService apiAuthenticationAuditService)
            : base(options, logger, encoder)
        {
            _apiTokenService = apiTokenService;
            _apiAuthenticationAuditService = apiAuthenticationAuditService;
        }

        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(Options.HeaderName, out var headerValues))
                return AuthenticateResult.NoResult();

            var token = headerValues.FirstOrDefault();
            if (string.IsNullOrWhiteSpace(token))
                return AuthenticateResult.NoResult();

            var result = await _apiTokenService.ValidateAsync(token, Context.RequestAborted);
            if (result is null || !result.Success)
            {
                await _apiAuthenticationAuditService.LogFailureAsync(Context, "Invalid API token.", Context.RequestAborted);
                return AuthenticateResult.Fail("Invalid API token.");
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, result.TokenId.ToString()),
                new Claim(ClaimTypes.Name, result.Name),
                new Claim("auth_type", "api_token")
            };

            if (result.TenantId.HasValue)
            {
                claims.Add(new Claim("tenant_id", result.TenantId.Value.ToString()));
            }

            foreach (var role in result.Roles)
                claims.Add(new Claim(ClaimTypes.Role, role));

            foreach (var scope in result.Scopes)
                claims.Add(new Claim("scope", scope));

            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);

            await _apiAuthenticationAuditService.LogSuccessAsync(Context, result, Context.RequestAborted);

            return AuthenticateResult.Success(ticket);
        }

        protected override Task HandleChallengeAsync(AuthenticationProperties properties)
        {
            Response.StatusCode = StatusCodes.Status401Unauthorized;
            Response.ContentType = "application/json";
            return Response.WriteAsync("""
        {"error":"unauthorized","error_description":"API token is missing or invalid."}
        """);
        }

        protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
        {
            _ = _apiAuthenticationAuditService.LogForbiddenAsync(Context, Context.RequestAborted);
            Response.StatusCode = StatusCodes.Status403Forbidden;
            Response.ContentType = "application/json";
            return Response.WriteAsync("""
        {"error":"forbidden","error_description":"Insufficient permissions."}
        """);
        }
    }
}
