using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using scp.filestorage.Data.Models;
using scp.filestorage.Security;
using scp.filestorage.Services.Auth;
using SCP.StorageFSC.Data.Dto;
using SCP.StorageFSC.InterfacesService;
using SCP.StorageFSC.Security;
using System.Security.Claims;
using FscAuthenticationService = scp.filestorage.Services.Auth.IAuthenticationService;

namespace SCP.StorageFSC.Controllers
{
    [ApiController]
    [Route("auth")]
    public sealed class AuthController : ControllerBase
    {
        private readonly FscAuthenticationService _authenticationService;
        private readonly IUserAuthenticationAuditService _userAuthenticationAuditService;

        public AuthController(
            FscAuthenticationService authenticationService,
            IUserAuthenticationAuditService userAuthenticationAuditService)
        {
            _authenticationService = authenticationService;
            _userAuthenticationAuditService = userAuthenticationAuditService;
        }

        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login(
            [FromBody] LoginRequest request,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Login))
                return BadRequest(ApiErrorResponse.Create(HttpContext, "ValidationError", "Login is required."));

            if (string.IsNullOrWhiteSpace(request.Password))
                return BadRequest(ApiErrorResponse.Create(HttpContext, "ValidationError", "Password is required."));

            var result = await _authenticationService.LoginAsync(
                new scp.filestorage.Services.Auth.LoginRequest
                {
                    Login = request.Login,
                    Password = request.Password,
                    TwoFactorCode = request.TwoFactorCode,
                    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                    UserAgent = Request.Headers.UserAgent.FirstOrDefault()
                },
                cancellationToken);

            await _userAuthenticationAuditService.LogPasswordLoginAsync(
                HttpContext,
                request.Login,
                result,
                cancellationToken);

            if (result.RequiresTwoFactor)
            {
                return Ok(new LoginChallengeResponse
                {
                    RequiresTwoFactor = true,
                    TwoFactorMethod = result.TwoFactorMethod.ToString(),
                    ChallengeToken = result.ChallengeToken,
                    ChallengeExpiresUtc = result.ChallengeExpiresUtc
                });
            }

            if (!result.Succeeded || result.UserId is null)
                return CreateLoginFailure(result.Status);

            var principal = CreatePrincipal(result.UserId.Value, result.UserName, result.Roles);
            await SignInAsync(principal, request.Remember);

            return Ok(ToMeResponse(principal));
        }

        [HttpPost("two-factor")]
        [AllowAnonymous]
        public async Task<IActionResult> VerifyTwoFactor(
            [FromBody] VerifyTwoFactorLoginRequest request,
            CancellationToken cancellationToken)
        {
            var result = await _authenticationService.VerifyTwoFactorAsync(
                new VerifyTwoFactorRequest
                {
                    ChallengeToken = request.ChallengeToken,
                    Code = request.Code,
                    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                    UserAgent = Request.Headers.UserAgent.FirstOrDefault()
                },
                cancellationToken);

            await _userAuthenticationAuditService.LogTwoFactorAsync(
                HttpContext,
                result,
                "TwoFactor",
                cancellationToken);

            return await CompleteTwoFactorSignInAsync(result, request.Remember);
        }

        [HttpPost("recovery-code")]
        [AllowAnonymous]
        public async Task<IActionResult> VerifyRecoveryCode(
            [FromBody] VerifyTwoFactorLoginRequest request,
            CancellationToken cancellationToken)
        {
            var result = await _authenticationService.VerifyRecoveryCodeAsync(
                new VerifyTwoFactorRequest
                {
                    ChallengeToken = request.ChallengeToken,
                    Code = request.Code,
                    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                    UserAgent = Request.Headers.UserAgent.FirstOrDefault()
                },
                cancellationToken);

            await _userAuthenticationAuditService.LogTwoFactorAsync(
                HttpContext,
                result,
                "RecoveryCode",
                cancellationToken);

            return await CompleteTwoFactorSignInAsync(result, request.Remember);
        }

        private async Task<IActionResult> CompleteTwoFactorSignInAsync(
            VerifyTwoFactorResult result,
            bool remember)
        {
            if (!result.Succeeded || result.UserId is null)
                return CreateTwoFactorFailure(result.Status);

            var principal = CreatePrincipal(result.UserId.Value, result.UserName, result.Roles);
            await SignInAsync(principal, remember);

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

        [HttpPost("change-password")]
        [Authorize(Policy = ApiTokenAuthenticationExtensions.WebUserOnlyPolicy)]
        public async Task<IActionResult> ChangePassword(
            [FromBody] ChangePasswordRequest request,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.OldPassword))
                return BadRequest(ApiErrorResponse.Create(HttpContext, "ValidationError", "Old password is required."));

            if (string.IsNullOrWhiteSpace(request.NewPassword))
                return BadRequest(ApiErrorResponse.Create(HttpContext, "ValidationError", "New password is required."));

            if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
                return Unauthorized(ApiErrorResponse.Create(HttpContext, "InvalidUser", "Authenticated user is invalid."));

            var changed = await _authenticationService.ChangePasswordAsync(
                userId,
                request.OldPassword,
                request.NewPassword,
                cancellationToken);

            if (!changed)
                return Unauthorized(ApiErrorResponse.Create(HttpContext, "InvalidCredentials", "Old password is invalid."));

            return NoContent();
        }

        private static MeResponse ToMeResponse(ClaimsPrincipal principal)
        {
            return new MeResponse
            {
                Identifier = principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? Guid.Empty.ToString(),
                Name = principal.FindFirstValue(ClaimTypes.Name) ?? string.Empty,
                IsAdmin = principal.IsInRole("Admin"),
                TenantId = Guid.TryParse(principal.FindFirstValue("tenant_id"), out var tenantId)
                    ? tenantId
                    : null,
                Scopes = principal.FindAll("scope").Select(claim => claim.Value).ToArray()
            };
        }

        private async Task SignInAsync(ClaimsPrincipal principal, bool remember)
        {
            await HttpContext.SignInAsync(
                ApiTokenAuthenticationExtensions.CookieScheme,
                principal,
                new AuthenticationProperties
                {
                    IsPersistent = remember,
                    IssuedUtc = DateTimeOffset.UtcNow
                });
        }

        internal static ClaimsPrincipal CreatePrincipal(
            Guid userId,
            string userName,
            IReadOnlyList<string> roles)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, userId.ToString()),
                new(ClaimTypes.Name, userName),
                new("auth_type", AuthType.WebApp)
            };

            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));

                if (string.Equals(role, SystemRoles.Administrator, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
                {
                    claims.Add(new Claim(ClaimTypes.Role, "Admin"));
                    claims.Add(new Claim("scope", "admin"));
                }
            }

            return new ClaimsPrincipal(new ClaimsIdentity(claims, ApiTokenAuthenticationExtensions.CookieScheme));
        }

        private IActionResult CreateLoginFailure(AuthLoginStatus status)
        {
            return status switch
            {
                AuthLoginStatus.InvalidCredentials or AuthLoginStatus.UserNotFound =>
                    Unauthorized(ApiErrorResponse.Create(HttpContext, "InvalidCredentials", "Login or password is invalid.")),
                AuthLoginStatus.UserInactive =>
                    ForbidWithError("UserInactive", "User account is inactive."),
                AuthLoginStatus.UserLocked =>
                    ForbidWithError("UserLocked", "User account is locked."),
                AuthLoginStatus.PasswordExpired =>
                    StatusCode(StatusCodes.Status403Forbidden, ApiErrorResponse.Create(HttpContext, "PasswordExpired", "Password is expired.")),
                AuthLoginStatus.PasswordChangeRequired =>
                    StatusCode(StatusCodes.Status403Forbidden, ApiErrorResponse.Create(HttpContext, "PasswordChangeRequired", "Password change is required.")),
                _ =>
                    Unauthorized(ApiErrorResponse.Create(HttpContext, "InvalidCredentials", "Login or password is invalid."))
            };
        }

        private IActionResult CreateTwoFactorFailure(TwoFactorVerifyStatus status)
        {
            return status switch
            {
                TwoFactorVerifyStatus.InvalidChallenge =>
                    Unauthorized(ApiErrorResponse.Create(HttpContext, "InvalidChallenge", "Two-factor challenge is invalid.")),
                TwoFactorVerifyStatus.ChallengeExpired =>
                    Unauthorized(ApiErrorResponse.Create(HttpContext, "ChallengeExpired", "Two-factor challenge has expired.")),
                TwoFactorVerifyStatus.ChallengeBlocked =>
                    ForbidWithError("ChallengeBlocked", "Two-factor challenge is blocked."),
                TwoFactorVerifyStatus.InvalidCode =>
                    Unauthorized(ApiErrorResponse.Create(HttpContext, "InvalidCode", "Two-factor code is invalid.")),
                TwoFactorVerifyStatus.MethodUnavailable =>
                    ForbidWithError("MethodUnavailable", "Two-factor method is unavailable."),
                TwoFactorVerifyStatus.UserInactive =>
                    ForbidWithError("UserInactive", "User account is inactive."),
                TwoFactorVerifyStatus.UserLocked =>
                    ForbidWithError("UserLocked", "User account is locked."),
                _ =>
                    Unauthorized(ApiErrorResponse.Create(HttpContext, "InvalidCode", "Two-factor code is invalid."))
            };
        }

        private ObjectResult ForbidWithError(string code, string message)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiErrorResponse.Create(HttpContext, code, message));
        }
    }

    public sealed class VerifyTwoFactorLoginRequest
    {
        public string ChallengeToken { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public bool Remember { get; set; }
    }

    public sealed class LoginChallengeResponse
    {
        public bool RequiresTwoFactor { get; set; }
        public string TwoFactorMethod { get; set; } = string.Empty;
        public string? ChallengeToken { get; set; }
        public DateTime? ChallengeExpiresUtc { get; set; }
    }

    public sealed class MeResponse
    {
        public string Identifier { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public bool IsAdmin { get; set; }
        public Guid? TenantId { get; set; }
        public string[] Scopes { get; set; } = [];
    }

    public sealed class ChangePasswordRequest
    {
        public string OldPassword { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }
}
