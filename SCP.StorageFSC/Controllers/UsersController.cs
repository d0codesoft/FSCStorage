using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SCP.StorageFSC.Data.Dto;
using SCP.StorageFSC.InterfacesService;
using SCP.StorageFSC.Security;
using scp.filestorage.Services;
using scp.filestorage.Services.Auth;
using FscAuthenticationService = scp.filestorage.Services.Auth.IAuthenticationService;

namespace scp.filestorage.Controllers
{
    [ApiController]
    [Route("ui-api/users")]
    [Authorize(Policy = ApiTokenAuthenticationExtensions.AdminOnlyPolicy)]
    public sealed class UsersController : ControllerBase
    {
        private const string AuthenticatorIssuer = "FSCStorage";

        private readonly ITenantStorageService _tenantStorageService;
        private readonly IFileStorageBackgroundTaskQueue _backgroundTaskQueue;
        private readonly FscAuthenticationService _authenticationService;
        private readonly ILogger<UsersController> _logger;

        public UsersController(
            ITenantStorageService tenantStorageService,
            IFileStorageBackgroundTaskQueue backgroundTaskQueue,
            FscAuthenticationService authenticationService,
            ILogger<UsersController> logger)
        {
            _tenantStorageService = tenantStorageService;
            _backgroundTaskQueue = backgroundTaskQueue;
            _authenticationService = authenticationService;
            _logger = logger;
        }

        [HttpGet]
        [ProducesResponseType(typeof(IReadOnlyList<UserManagementDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetUsers(CancellationToken cancellationToken)
        {
            return Ok(await _tenantStorageService.GetUsersAsync(cancellationToken));
        }

        [HttpGet("{userId:guid}")]
        [ProducesResponseType(typeof(UserManagementDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetUser(Guid userId, CancellationToken cancellationToken)
        {
            var result = await _tenantStorageService.GetUserAsync(userId, cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }

        [HttpGet("tenants")]
        [ProducesResponseType(typeof(IReadOnlyList<UserTenantsDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetUsersWithTenants(CancellationToken cancellationToken)
        {
            return Ok(await _tenantStorageService.GetUsersWithTenantsAsync(cancellationToken));
        }

        [HttpPost]
        [ProducesResponseType(typeof(UserManagementDto), StatusCodes.Status201Created)]
        public async Task<IActionResult> CreateUser(
            [FromBody] CreateUserRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                var result = await _tenantStorageService.CreateUserAsync(request, cancellationToken);
                return CreatedAtAction(nameof(GetUser), new { userId = result.UserId }, result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { error = ex.Message });
            }
        }

        [HttpPut("{userId:guid}")]
        [ProducesResponseType(typeof(UserManagementDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateUser(
            Guid userId,
            [FromBody] UpdateUserProfileRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                var result = await _tenantStorageService.UpdateUserProfileAsync(userId, request, cancellationToken);
                return result is null ? NotFound() : Ok(result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { error = ex.Message });
            }
        }

        [HttpDelete("{userId:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteUser(Guid userId, CancellationToken cancellationToken)
        {
            try
            {
                if (!await _tenantStorageService.DeleteUserAsync(userId, cancellationToken))
                    return NotFound();

                await _backgroundTaskQueue.QueueAsync(
                    FileStorageBackgroundTask.CleanupDeletedTenantFiles(),
                    cancellationToken);

                return NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { error = ex.Message });
            }
        }

        [HttpPost("{userId:guid}/activate")]
        public async Task<IActionResult> ActivateUser(Guid userId, CancellationToken cancellationToken)
            => await NoContentOrNotFoundAsync(_tenantStorageService.ActivateUserAsync(userId, cancellationToken));

        [HttpPost("{userId:guid}/deactivate")]
        public async Task<IActionResult> DeactivateUser(Guid userId, CancellationToken cancellationToken)
            => await NoContentOrNotFoundAsync(_tenantStorageService.DeactivateUserAsync(userId, cancellationToken));

        [HttpPost("{userId:guid}/lock")]
        public async Task<IActionResult> LockUser(
            Guid userId,
            [FromBody] LockUserRequest request,
            CancellationToken cancellationToken)
            => await NoContentOrNotFoundAsync(_tenantStorageService.LockUserAsync(userId, request.LockedUntilUtc, cancellationToken));

        [HttpPost("{userId:guid}/unlock")]
        public async Task<IActionResult> UnlockUser(Guid userId, CancellationToken cancellationToken)
            => await NoContentOrNotFoundAsync(_tenantStorageService.UnlockUserAsync(userId, cancellationToken));

        [HttpPost("{userId:guid}/block")]
        [ApiExplorerSettings(IgnoreApi = true)]
        public Task<IActionResult> BlockUserLegacy(Guid userId, CancellationToken cancellationToken)
        {
            return LockUser(
                userId,
                new LockUserRequest { LockedUntilUtc = DateTime.UtcNow.AddYears(100) },
                cancellationToken);
        }

        [HttpPost("{userId:guid}/unblock")]
        [ApiExplorerSettings(IgnoreApi = true)]
        public Task<IActionResult> UnblockUserLegacy(Guid userId, CancellationToken cancellationToken)
        {
            return UnlockUser(userId, cancellationToken);
        }

        [HttpPost("{userId:guid}/reset-failed-login-count")]
        public async Task<IActionResult> ResetFailedLoginCount(Guid userId, CancellationToken cancellationToken)
            => await NoContentOrNotFoundAsync(_tenantStorageService.ResetFailedLoginCountAsync(userId, cancellationToken));

        [HttpPost("{userId:guid}/password/change")]
        public async Task<IActionResult> ChangePassword(
            Guid userId,
            [FromBody] ChangeUserPasswordRequest request,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.CurrentPassword) || string.IsNullOrWhiteSpace(request.NewPassword))
                return BadRequest(new { error = "Current password and new password are required." });

            return await _authenticationService.ChangePasswordAsync(
                userId,
                request.CurrentPassword,
                request.NewPassword,
                cancellationToken)
                ? NoContent()
                : NotFound();
        }

        [HttpPost("{userId:guid}/password/reset")]
        public async Task<IActionResult> ResetPassword(
            Guid userId,
            [FromBody] ResetUserPasswordRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                return await NoContentOrNotFoundAsync(_tenantStorageService.ResetUserPasswordAsync(userId, request, cancellationToken));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost("{userId:guid}/password/expire")]
        public async Task<IActionResult> ExpirePassword(Guid userId, CancellationToken cancellationToken)
            => await NoContentOrNotFoundAsync(_tenantStorageService.ExpireUserPasswordAsync(userId, cancellationToken));

        [HttpPost("{userId:guid}/email/change")]
        public async Task<IActionResult> ChangeEmail(
            Guid userId,
            [FromBody] ChangeUserEmailRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                return await NoContentOrNotFoundAsync(_tenantStorageService.ChangeUserEmailAsync(userId, request.Email, cancellationToken));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { error = ex.Message });
            }
        }

        [HttpPost("{userId:guid}/email/confirm")]
        public async Task<IActionResult> ConfirmEmail(Guid userId, CancellationToken cancellationToken)
            => await NoContentOrNotFoundAsync(_tenantStorageService.SetUserEmailConfirmedAsync(userId, true, cancellationToken));

        [HttpPost("{userId:guid}/email/unconfirm")]
        public async Task<IActionResult> UnconfirmEmail(Guid userId, CancellationToken cancellationToken)
            => await NoContentOrNotFoundAsync(_tenantStorageService.SetUserEmailConfirmedAsync(userId, false, cancellationToken));

        [HttpPost("{userId:guid}/email/send-confirmation")]
        public IActionResult SendEmailConfirmation(Guid userId)
        {
            _logger.LogInformation("Email confirmation requested for user {UserId}.", userId);
            return Accepted();
        }

        [HttpPost("{userId:guid}/phone/change")]
        public async Task<IActionResult> ChangePhone(
            Guid userId,
            [FromBody] ChangeUserPhoneRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                return await NoContentOrNotFoundAsync(_tenantStorageService.ChangeUserPhoneAsync(userId, request.PhoneNumber, cancellationToken));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost("{userId:guid}/phone/confirm")]
        public async Task<IActionResult> ConfirmPhone(Guid userId, CancellationToken cancellationToken)
            => await NoContentOrNotFoundAsync(_tenantStorageService.SetUserPhoneConfirmedAsync(userId, true, cancellationToken));

        [HttpPost("{userId:guid}/phone/unconfirm")]
        public async Task<IActionResult> UnconfirmPhone(Guid userId, CancellationToken cancellationToken)
            => await NoContentOrNotFoundAsync(_tenantStorageService.SetUserPhoneConfirmedAsync(userId, false, cancellationToken));

        [HttpPost("{userId:guid}/phone/send-confirmation")]
        public IActionResult SendPhoneConfirmation(Guid userId)
        {
            _logger.LogInformation("Phone confirmation requested for user {UserId}.", userId);
            return Accepted();
        }

        [HttpGet("{userId:guid}/2fa/status")]
        public async Task<IActionResult> GetTwoFactorStatus(Guid userId, CancellationToken cancellationToken)
        {
            var result = await _tenantStorageService.GetUserTwoFactorStatusAsync(userId, cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }

        [HttpPost("{userId:guid}/2fa/setup-authenticator")]
        public async Task<IActionResult> SetupAuthenticator(Guid userId, CancellationToken cancellationToken)
        {
            var result = await _authenticationService.BeginEnableAuthenticatorAsync(userId, AuthenticatorIssuer, cancellationToken);
            return result.Status == TwoFactorSetupStatus.UserNotFound ? NotFound() : Ok(result);
        }

        [HttpPost("{userId:guid}/2fa/confirm-authenticator")]
        public async Task<IActionResult> ConfirmAuthenticator(
            Guid userId,
            [FromBody] ConfirmAuthenticatorRequest request,
            CancellationToken cancellationToken)
        {
            var status = await _authenticationService.ConfirmEnableAuthenticatorAsync(userId, request.Code, cancellationToken);
            return status switch
            {
                TwoFactorSetupStatus.Success => NoContent(),
                TwoFactorSetupStatus.UserNotFound => NotFound(),
                TwoFactorSetupStatus.InvalidCode => BadRequest(new { error = "Authenticator code is invalid." }),
                _ => Conflict(new { error = status.ToString() })
            };
        }

        [HttpPost("{userId:guid}/2fa/enable")]
        public async Task<IActionResult> EnableTwoFactor(Guid userId, CancellationToken cancellationToken)
            => await NoContentOrNotFoundAsync(_tenantStorageService.EnableUserTwoFactorAsync(userId, cancellationToken));

        [HttpPost("{userId:guid}/2fa/disable")]
        public async Task<IActionResult> DisableTwoFactor(Guid userId, CancellationToken cancellationToken)
            => await NoContentOrNotFoundAsync(_tenantStorageService.DisableUserTwoFactorAsync(userId, cancellationToken));

        [HttpPost("{userId:guid}/2fa/set-preferred-method")]
        public async Task<IActionResult> SetPreferredTwoFactorMethod(
            Guid userId,
            [FromBody] SetPreferredTwoFactorMethodRequest request,
            CancellationToken cancellationToken)
            => await NoContentOrNotFoundAsync(_tenantStorageService.SetUserPreferredTwoFactorMethodAsync(userId, request, cancellationToken));

        [HttpPost("{userId:guid}/2fa/set-required-for-every-login")]
        public async Task<IActionResult> SetRequiredForEveryLogin(
            Guid userId,
            [FromBody] SetTwoFactorRequiredRequest request,
            CancellationToken cancellationToken)
            => await NoContentOrNotFoundAsync(_tenantStorageService.SetUserTwoFactorRequiredForEveryLoginAsync(userId, request.Required, cancellationToken));

        [HttpPost("{userId:guid}/2fa/reset")]
        public async Task<IActionResult> ResetTwoFactor(Guid userId, CancellationToken cancellationToken)
            => await NoContentOrNotFoundAsync(_tenantStorageService.ResetUserTwoFactorAsync(userId, cancellationToken));

        [HttpPost("{userId:guid}/2fa/recovery-codes/regenerate")]
        public async Task<IActionResult> RegenerateRecoveryCodes(Guid userId, CancellationToken cancellationToken)
        {
            var codes = await _tenantStorageService.RegenerateUserRecoveryCodesAsync(userId, cancellationToken);
            return codes.Length == 0 ? NotFound() : Ok(new { recoveryCodes = codes });
        }

        [HttpPost("{userId:guid}/security-stamp/refresh")]
        public async Task<IActionResult> RefreshSecurityStamp(Guid userId, CancellationToken cancellationToken)
            => await NoContentOrNotFoundAsync(_tenantStorageService.RefreshUserSecurityStampAsync(userId, cancellationToken));

        [HttpPost("{userId:guid}/sessions/revoke")]
        [HttpPost("{userId:guid}/sessions/revoke-all")]
        public async Task<IActionResult> RevokeSessions(Guid userId, CancellationToken cancellationToken)
            => await NoContentOrNotFoundAsync(_tenantStorageService.RefreshUserSecurityStampAsync(userId, cancellationToken));

        [HttpGet("{userId:guid}/sessions")]
        public async Task<IActionResult> GetSessions(Guid userId, CancellationToken cancellationToken)
        {
            var result = await _tenantStorageService.GetUserSessionsAsync(userId, cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }

        [HttpGet("{userId:guid}/login-history")]
        public async Task<IActionResult> GetLoginHistory(Guid userId, CancellationToken cancellationToken)
        {
            var result = await _tenantStorageService.GetUserLoginHistoryAsync(userId, cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }

        [HttpGet("{userId:guid}/security-events")]
        [HttpGet("{userId:guid}/audit")]
        public async Task<IActionResult> GetSecurityEvents(Guid userId, CancellationToken cancellationToken)
        {
            var result = await _tenantStorageService.GetUserSecurityEventsAsync(userId, cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }

        [HttpGet("check-name")]
        public async Task<IActionResult> CheckName([FromQuery] string name, CancellationToken cancellationToken)
        {
            return Ok(new UniqueCheckResultDto
            {
                IsUnique = await _tenantStorageService.IsUserNameUniqueAsync(name, cancellationToken: cancellationToken)
            });
        }

        [HttpGet("check-email")]
        public async Task<IActionResult> CheckEmail([FromQuery] string email, CancellationToken cancellationToken)
        {
            return Ok(new UniqueCheckResultDto
            {
                IsUnique = await _tenantStorageService.IsUserEmailUniqueAsync(email, cancellationToken: cancellationToken)
            });
        }

        private static async Task<IActionResult> NoContentOrNotFoundAsync(Task<bool> operation)
        {
            return await operation ? new NoContentResult() : new NotFoundResult();
        }
    }
}
