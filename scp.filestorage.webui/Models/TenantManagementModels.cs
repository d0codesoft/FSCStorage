using System.ComponentModel.DataAnnotations;

namespace scp.filestorage.webui.Models
{
    public enum TwoFactorMethodType
    {
        None = 0,
        AuthenticatorApp = 1,
        Email = 2,
        Sms = 3
    }

    public enum TwoFactorSetupStatus
    {
        Success = 0,
        UserNotFound = 1,
        UserInactive = 2,
        MethodAlreadyExists = 3,
        InvalidCode = 4
    }

    public sealed class TenantViewModel
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

    public sealed class TenantEditorModel
    {
        [Required(ErrorMessage = "Owner user is required.")]
        public Guid UserId { get; set; }

        [Required(ErrorMessage = "Instance name is required.")]
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
    }

    public sealed class TenantUpsertRequest
    {
        public Guid UserId { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
    }

    public sealed class ApiTokenViewModel
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

        public string ScopesLabel =>
            string.Join(", ", GetScopes().DefaultIfEmpty("none"));

        private IEnumerable<string> GetScopes()
        {
            if (CanRead)
                yield return "read";

            if (CanWrite)
                yield return "write";

            if (CanDelete)
                yield return "delete";

            if (IsAdmin)
                yield return "admin";
        }
    }

    public sealed class StoredTenantFileViewModel
    {
        public Guid TenantFileId { get; set; }
        public Guid FileGuid { get; set; }
        public Guid TenantId { get; set; }
        public Guid StoredFileId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string? Category { get; set; }
        public string? ExternalKey { get; set; }
        public string? ContentType { get; set; }
        public int StateCompress { get; set; }
        public long FileSize { get; set; }
        public string Sha256 { get; set; } = string.Empty;
        public string Crc32 { get; set; } = string.Empty;
        public DateTime CreatedUtc { get; set; }
    }

    public sealed class ApiTokenEditorModel
    {
        [Required(ErrorMessage = "Token name is required.")]
        public string Name { get; set; } = string.Empty;
        public bool CanRead { get; set; } = true;
        public bool CanWrite { get; set; }
        public bool CanDelete { get; set; }
        public bool IsAdmin { get; set; }
        public bool IsActive { get; set; } = true;
        public string? ExpiresUtcText { get; set; }
    }

    public sealed class CreateApiTokenRequestModel
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

    public sealed class CreateTenantApiTokenRequestModel
    {
        public string Name { get; set; } = string.Empty;
        public bool CanRead { get; set; } = true;
        public bool CanWrite { get; set; }
        public bool CanDelete { get; set; }
        public bool IsAdmin { get; set; }
        public DateTime? ExpiresUtc { get; set; }
    }

    public sealed class UpdateApiTokenRequestModel
    {
        public string Name { get; set; } = string.Empty;
        public bool CanRead { get; set; } = true;
        public bool CanWrite { get; set; }
        public bool CanDelete { get; set; }
        public bool IsAdmin { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime? ExpiresUtc { get; set; }
    }

    public sealed class CreatedApiTokenViewModel
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string TokenPrefix { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
        public string[] Scopes { get; set; } = [];
        public bool IsAdmin { get; set; }
        public DateTime? ExpiresUtc { get; set; }
    }

    public sealed class UserTenantsViewModel
    {
        public Guid UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string? Email { get; set; }
        public bool EmailConfirmed { get; set; }
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
        public IReadOnlyList<TenantViewModel> Tenants { get; set; } = [];
    }

    public sealed class UserApiTokenViewModel
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

        public string ScopesLabel =>
            string.Join(", ", GetScopes().DefaultIfEmpty("none"));

        private IEnumerable<string> GetScopes()
        {
            if (CanRead)
                yield return "read";

            if (CanWrite)
                yield return "write";

            if (CanDelete)
                yield return "delete";

            if (IsAdmin)
                yield return "admin";
        }
    }

    public sealed class UserManagementViewModel
    {
        public Guid UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string? Email { get; set; }
        public bool EmailConfirmed { get; set; }
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
        public IReadOnlyList<TenantViewModel> Tenants { get; set; } = [];
        public IReadOnlyList<UserApiTokenViewModel> ApiTokens { get; set; } = [];
    }

    public sealed class UserEditorModel
    {
        [Required(ErrorMessage = "User name is required.")]
        public string Name { get; set; } = string.Empty;

        [EmailAddress(ErrorMessage = "Enter a valid email address.")]
        public string? Email { get; set; }

        public string? Password { get; set; }
        public bool IsActive { get; set; } = true;
        public bool IsAdmin { get; set; }
    }

    public sealed class UserProfileEditorModel
    {
        [Required(ErrorMessage = "User name is required.")]
        public string Name { get; set; } = string.Empty;

        [EmailAddress(ErrorMessage = "Enter a valid email address.")]
        public string? Email { get; set; }

        public string? PhoneNumber { get; set; }
        public string? ExternalUserId { get; set; }
        public string? Comment { get; set; }
    }

    public sealed class CreateUserRequestModel
    {
        public string Name { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string Password { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public bool IsAdmin { get; set; }
    }

    public sealed class UpdateUserRequestModel
    {
        public string Name { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
        public bool PhoneNumberConfirmed { get; set; }
        public string? Password { get; set; }
        public bool IsActive { get; set; } = true;
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
        public DateTime? PasswordExpiresUtc { get; init; }
        public string? ExternalUserId { get; init; }
        public string? Comment { get; set; }
        public bool IsAdmin { get; set; }
    }

    public sealed class UpdateUserProfileRequestModel
    {
        public string Name { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
        public string? ExternalUserId { get; set; }
        public string? Comment { get; set; }
    }

    public sealed class UserTwoFactorStatusViewModel
    {
        public Guid UserId { get; set; }
        public bool TwoFactorEnabled { get; set; }
        public bool TwoFactorRequiredForEveryLogin { get; set; }
        public TwoFactorMethodType PreferredTwoFactorMethod { get; set; }
        public DateTime? TwoFactorEnabledUtc { get; set; }
        public DateTime? TwoFactorLastUsedUtc { get; set; }
        public IReadOnlyList<UserTwoFactorMethodViewModel> Methods { get; set; } = [];
    }

    public sealed class UserTwoFactorMethodViewModel
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

    public sealed class AuthenticatorSetupViewModel
    {
        public TwoFactorSetupStatus Status { get; set; }
        public string? Secret { get; set; }
        public string? OtpAuthUri { get; set; }
        public string QrCodePngBase64 { get; set; } = string.Empty;
    }

    public sealed class ConfirmAuthenticatorRequestModel
    {
        public string Code { get; set; } = string.Empty;
    }

    public sealed class ConfirmUserEmailRequestModel
    {
        public string Code { get; set; } = string.Empty;
    }

    public sealed class UserLoginHistoryViewModel
    {
        public DateTime? LastLoginUtc { get; set; }
        public string? LastLoginIpAddress { get; set; }
        public DateTime? LastFailedLoginUtc { get; set; }
        public int FailedLoginCount { get; set; }
        public DateTime? TwoFactorLastUsedUtc { get; set; }
        public DateTime? PasswordChangedUtc { get; set; }
    }

    public sealed class UserSecurityEventViewModel
    {
        public string EventType { get; set; } = string.Empty;
        public DateTime? OccurredUtc { get; set; }
        public string? Description { get; set; }
    }

    public sealed class ResetUserPasswordRequestModel
    {
        public string NewPassword { get; set; } = string.Empty;
        public bool MustChangePassword { get; set; } = true;
    }

    public sealed class SetPreferredTwoFactorMethodRequestModel
    {
        public TwoFactorMethodType PreferredTwoFactorMethod { get; set; } = TwoFactorMethodType.AuthenticatorApp;
    }

    public sealed class SetTwoFactorRequiredRequestModel
    {
        public bool Required { get; set; }
    }

    public sealed class ChangePasswordRequestModel
    {
        public string OldPassword { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }
}
