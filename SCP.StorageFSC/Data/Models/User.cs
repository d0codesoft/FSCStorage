using scp.filestorage.Data.Models;

namespace SCP.StorageFSC.Data.Models
{
    /// <summary>
    /// Application user.
    /// </summary>
    public sealed class User : EntityBase
    {
        /// <summary>
        /// User display name or login name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Normalized user name used for case-insensitive search and uniqueness checks.
        /// </summary>
        public string NormalizedName { get; set; } = string.Empty;

        /// <summary>
        /// Optional user email address used for notifications, password recovery, and email-based two-factor authentication.
        /// </summary>
        public string? Email { get; set; }

        /// <summary>
        /// Normalized email address used for case-insensitive search and uniqueness checks.
        /// </summary>
        public string? NormalizedEmail { get; set; }

        /// <summary>
        /// Indicates whether the email address has been confirmed by the user.
        /// </summary>
        public bool EmailConfirmed { get; set; }

        /// <summary>
        /// Optional phone number used for SMS-based two-factor authentication.
        /// </summary>
        public string? PhoneNumber { get; set; }

        /// <summary>
        /// Indicates whether the phone number has been confirmed by the user.
        /// </summary>
        public bool PhoneNumberConfirmed { get; set; }

        /// <summary>
        /// Password hash used for authentication. Plain text passwords must never be stored.
        /// </summary>
        public string PasswordHash { get; set; } = string.Empty;

        /// <summary>
        /// UTC date and time when the password was last changed.
        /// </summary>
        public DateTime? PasswordChangedUtc { get; set; }

        /// <summary>
        /// Random security value used to invalidate sessions, refresh tokens, and authentication cookies.
        /// </summary>
        public string SecurityStamp { get; set; } = Guid.NewGuid().ToString("N");

        /// <summary>
        /// Indicates whether the user account is active and allowed to authenticate.
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Indicates whether the user account is locked.
        /// </summary>
        public bool IsLocked { get; set; }

        /// <summary>
        /// UTC date and time until which the user account is locked.
        /// </summary>
        public DateTime? LockedUntilUtc { get; set; }

        /// <summary>
        /// Number of consecutive failed password login attempts.
        /// </summary>
        public int FailedLoginCount { get; set; }

        /// <summary>
        /// UTC date and time of the last failed password login attempt.
        /// </summary>
        public DateTime? LastFailedLoginUtc { get; set; }

        /// <summary>
        /// UTC date and time of the last successful full login.
        /// </summary>
        public DateTime? LastLoginUtc { get; set; }

        /// <summary>
        /// IP address used during the last successful full login.
        /// </summary>
        public string? LastLoginIpAddress { get; set; }

        /// <summary>
        /// Indicates whether two-factor authentication is enabled for the user.
        /// </summary>
        public bool TwoFactorEnabled { get; set; }

        /// <summary>
        /// Indicates whether two-factor authentication is required on every successful password login.
        /// </summary>
        public bool TwoFactorRequiredForEveryLogin { get; set; } = true;

        /// <summary>
        /// Preferred two-factor authentication method used when several methods are available.
        /// </summary>
        public TwoFactorMethodType PreferredTwoFactorMethod { get; set; } = TwoFactorMethodType.AuthenticatorApp;

        /// <summary>
        /// UTC date and time when two-factor authentication was enabled.
        /// </summary>
        public DateTime? TwoFactorEnabledUtc { get; set; }

        /// <summary>
        /// UTC date and time when two-factor authentication was last successfully used.
        /// </summary>
        public DateTime? TwoFactorLastUsedUtc { get; set; }

        /// <summary>
        /// Indicates whether the user must change the password during the next login.
        /// </summary>
        public bool MustChangePassword { get; set; }

        /// <summary>
        /// UTC date and time after which the password should be considered expired.
        /// </summary>
        public DateTime? PasswordExpiresUtc { get; set; }

        /// <summary>
        /// Optional external system identifier used to map this user to another identity source.
        /// </summary>
        public string? ExternalUserId { get; set; }

        /// <summary>
        /// Optional administrative comment.
        /// </summary>
        public string? Comment { get; set; }
    }
}
