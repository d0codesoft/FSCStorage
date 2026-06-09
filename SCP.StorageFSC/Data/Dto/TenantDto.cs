using scp.filestorage.Data.Models;

namespace SCP.StorageFSC.Data.Dto
{
    public sealed class CreateTenantRequest
    {
        public Guid UserId { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public sealed class UpdateTenantRequest
    {
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
    }

    public sealed class TenantDto
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string UserEmail { get; set; } = string.Empty;
        public bool IsActiveUser { get; set; } = true;
        public Guid TenantGuid { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime CreatedUtc { get; set; }
        public DateTime? UpdatedUtc { get; set; }
    }

    public sealed class CreateApiTokenRequest
    {
        public Guid UserId { get; set; }
        public Guid TenantId { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool CanRead { get; set; } = true;
        public bool CanWrite { get; set; }
        public bool CanDelete { get; set; }
        public bool IsAdmin { get; set; }
        public DateTime? ExpiresUtc { get; set; }
    }

    public sealed class CreateTenantApiTokenRequest
    {
        public string Name { get; set; } = string.Empty;
        public bool CanRead { get; set; } = true;
        public bool CanWrite { get; set; }
        public bool CanDelete { get; set; }
        public bool IsAdmin { get; set; }
        public DateTime? ExpiresUtc { get; set; }
    }

    public sealed class UpdateApiTokenRequest
    {
        public string Name { get; set; } = string.Empty;
        public bool CanRead { get; set; } = true;
        public bool CanWrite { get; set; }
        public bool CanDelete { get; set; }
        public bool IsAdmin { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime? ExpiresUtc { get; set; }
    }

    public sealed class ApiTokenDto
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public Guid TenantId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string TokenPrefix { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public bool CanRead { get; set; }
        public bool CanWrite { get; set; }
        public bool CanDelete { get; set; }
        public bool IsAdmin { get; set; }
        public DateTime CreatedUtc { get; set; }
        public DateTime? LastUsedUtc { get; set; }
        public DateTime? ExpiresUtc { get; set; }
        public DateTime? RevokedUtc { get; set; }
    }

    public sealed class UserTenantsDto
    {
        public Guid UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
        public bool PhoneNumberConfirmed { get; set; }
        public bool IsActive { get; set; }
        public bool IsLocked { get; set; }
        public DateTime? LockedUntilUtc { get; init; }
        public int FailedLoginCount { get; init; }
        public DateTime? LastFailedLoginUtc { get; init; }
        public DateTime? LastLoginUtc { get; init; }
        public string? LastLoginIpAddress { get; init; }
        public bool TwoFactorEnabled { get; set; }
        public bool TwoFactorRequiredForEveryLogin { get; set; } = true;
        public TwoFactorMethodType PreferredTwoFactorMethod { get; set; } = TwoFactorMethodType.AuthenticatorApp;
        public DateTime? TwoFactorEnabledUtc { get; init; }
        public DateTime? TwoFactorLastUsedUtc { get; init; }
        public bool MustChangePassword { get; set; }
        public DateTime? PasswordChangedUtc { get; init; }
        public DateTime? PasswordExpiresUtc { get; init; }
        public string? ExternalUserId { get; init; }
        public string? Comment { get; set; }
        public IReadOnlyList<TenantDto> Tenants { get; set; } = [];
    }

    public sealed class CreateUserRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
        public string? TemporaryPassword { get; set; }
        public string Password { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public bool MustChangePassword { get; set; } = true;
        public bool IsAdmin { get; set; }
    }

    public class UpdateUserProfileRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
        public string? ExternalUserId { get; init; }
        public string? Comment { get; set; }
    }

    public sealed class UpdateUserRequest : UpdateUserProfileRequest
    {
    }

    public sealed class LockUserRequest
    {
        public DateTime LockedUntilUtc { get; set; }
        public string? Reason { get; set; }
    }

    public sealed class ChangeUserPasswordRequest
    {
        public string CurrentPassword { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }

    public sealed class ResetUserPasswordRequest
    {
        public string NewPassword { get; set; } = string.Empty;
        public bool MustChangePassword { get; set; } = true;
    }

    public sealed class ChangeUserEmailRequest
    {
        public string Email { get; set; } = string.Empty;
    }

    public sealed class ChangeUserPhoneRequest
    {
        public string PhoneNumber { get; set; } = string.Empty;
    }

    public sealed class SetPreferredTwoFactorMethodRequest
    {
        public TwoFactorMethodType PreferredTwoFactorMethod { get; set; } = TwoFactorMethodType.AuthenticatorApp;
    }

    public sealed class SetTwoFactorRequiredRequest
    {
        public bool Required { get; set; }
    }

    public sealed class ConfirmAuthenticatorRequest
    {
        public string Code { get; set; } = string.Empty;
    }

    public sealed class UserTwoFactorStatusDto
    {
        public Guid UserId { get; set; }
        public bool TwoFactorEnabled { get; set; }
        public bool TwoFactorRequiredForEveryLogin { get; set; }
        public TwoFactorMethodType PreferredTwoFactorMethod { get; set; }
        public DateTime? TwoFactorEnabledUtc { get; set; }
        public DateTime? TwoFactorLastUsedUtc { get; set; }
        public IReadOnlyList<UserTwoFactorMethodDto> Methods { get; set; } = [];
    }

    public sealed class UserTwoFactorMethodDto
    {
        public Guid Id { get; set; }
        public TwoFactorMethodType MethodType { get; set; }
        public bool IsEnabled { get; set; }
        public bool IsConfirmed { get; set; }
        public bool IsDefault { get; set; }
        public string? MaskedDestination { get; set; }
        public DateTime? ConfirmedUtc { get; set; }
        public DateTime? LastUsedUtc { get; set; }
    }

    public sealed class UserSessionDto
    {
        public string SessionId { get; set; } = string.Empty;
        public DateTime? CreatedUtc { get; set; }
        public DateTime? LastUsedUtc { get; set; }
        public string? IpAddress { get; set; }
        public string? UserAgent { get; set; }
    }

    public sealed class UserLoginHistoryDto
    {
        public DateTime? LastLoginUtc { get; set; }
        public string? LastLoginIpAddress { get; set; }
        public DateTime? LastFailedLoginUtc { get; set; }
        public int FailedLoginCount { get; set; }
        public DateTime? TwoFactorLastUsedUtc { get; set; }
        public DateTime? PasswordChangedUtc { get; set; }
    }

    public sealed class UserSecurityEventDto
    {
        public string EventType { get; set; } = string.Empty;
        public DateTime? OccurredUtc { get; set; }
        public string? Description { get; set; }
    }

    public sealed class UniqueCheckResultDto
    {
        public bool IsUnique { get; set; }
    }

    public sealed class UserApiTokenDto
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public Guid TenantId { get; set; }
        public string TenantName { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string TokenPrefix { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public bool CanRead { get; set; }
        public bool CanWrite { get; set; }
        public bool CanDelete { get; set; }
        public bool IsAdmin { get; set; }
        public DateTime CreatedUtc { get; set; }
        public DateTime? LastUsedUtc { get; set; }
        public DateTime? ExpiresUtc { get; set; }
        public DateTime? RevokedUtc { get; set; }
    }

    public sealed class UserManagementDto
    {
        public Guid UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
        public bool PhoneNumberConfirmed { get; set; }
        public bool IsActive { get; set; }
        public bool IsLocked { get; set; }
        public DateTime? LockedUntilUtc { get; init; }
        public int FailedLoginCount { get; init; }
        public DateTime? LastFailedLoginUtc { get; init; }
        public DateTime? LastLoginUtc { get; init; }
        public string? LastLoginIpAddress { get; init; }
        public bool TwoFactorEnabled { get; set; }
        public bool TwoFactorRequiredForEveryLogin { get; set; } = true;
        public TwoFactorMethodType PreferredTwoFactorMethod { get; set; } = TwoFactorMethodType.AuthenticatorApp;
        public DateTime? TwoFactorEnabledUtc { get; init; }
        public DateTime? TwoFactorLastUsedUtc { get; init; }
        public bool MustChangePassword { get; set; }
        public DateTime? PasswordChangedUtc { get; init; }
        public DateTime? PasswordExpiresUtc { get; init; }
        public string? ExternalUserId { get; init; }
        public string? Comment { get; set; }
        public bool IsAdmin { get; set; }
        public DateTime CreatedUtc { get; set; }
        public DateTime? UpdatedUtc { get; set; }
        public IReadOnlyList<TenantDto> Tenants { get; set; } = [];
        public IReadOnlyList<UserApiTokenDto> ApiTokens { get; set; } = [];
    }

    public sealed class CreatedApiTokenResult
    {
        public ApiTokenDto Token { get; set; } = new();
        public string PlainTextToken { get; set; } = string.Empty;
    }

    public sealed class CreatedApiTokenResponse
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string TokenPrefix { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
        public string[] Scopes { get; set; } = [];
        public bool IsAdmin { get; set; }
        public DateTime? ExpiresUtc { get; set; }

        public static CreatedApiTokenResponse FromResult(CreatedApiTokenResult result)
        {
            return new CreatedApiTokenResponse
            {
                Id = result.Token.Id,
                TenantId = result.Token.TenantId,
                Name = result.Token.Name,
                TokenPrefix = result.Token.TokenPrefix,
                Token = result.PlainTextToken,
                Scopes = ApiTokenScopes.FromPermissions(
                    result.Token.CanRead,
                    result.Token.CanWrite,
                    result.Token.CanDelete),
                IsAdmin = result.Token.IsAdmin,
                ExpiresUtc = result.Token.ExpiresUtc
            };
        }
    }

    public static class ApiTokenScopes
    {
        public static string[] FromPermissions(bool canRead, bool canWrite, bool canDelete)
        {
            var scopes = new List<string>(3);

            if (canRead)
                scopes.Add("files.read");

            if (canWrite)
                scopes.Add("files.write");

            if (canDelete)
                scopes.Add("files.delete");

            return scopes.ToArray();
        }
    }
}
