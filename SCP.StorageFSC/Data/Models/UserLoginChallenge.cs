using SCP.StorageFSC.Data.Models;

namespace scp.filestorage.Data.Models
{
    /// <summary>
    /// Pending login session that requires additional two-factor verification.
    /// </summary>
    public sealed class UserLoginChallenge : EntityBase
    {
        /// <summary>
        /// Identifier of the user who owns this pending login challenge.
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// Random challenge token hash used to continue the login process.
        /// Plain text challenge tokens must never be stored.
        /// </summary>
        public string ChallengeTokenHash { get; set; } = string.Empty;

        /// <summary>
        /// Two-factor authentication method selected for this login challenge.
        /// </summary>
        public TwoFactorMethodType MethodType { get; set; }

        /// <summary>
        /// Identifier of the two-factor authentication challenge linked to this login challenge.
        /// This is usually used for email or SMS verification.
        /// </summary>
        public Guid? TwoFactorChallengeId { get; set; }

        /// <summary>
        /// Current status of the login challenge.
        /// </summary>
        public UserLoginChallengeStatus Status { get; set; } = UserLoginChallengeStatus.Pending;

        /// <summary>
        /// UTC date and time when the login challenge expires.
        /// </summary>
        public DateTime ExpiresUtc { get; set; }

        /// <summary>
        /// UTC date and time when the login challenge was completed.
        /// </summary>
        public DateTime? CompletedUtc { get; set; }

        /// <summary>
        /// Number of failed verification attempts for this login challenge.
        /// </summary>
        public int FailedAttemptCount { get; set; }

        /// <summary>
        /// Maximum allowed failed verification attempts before the login challenge is blocked.
        /// </summary>
        public int MaxFailedAttemptCount { get; set; } = 5;

        /// <summary>
        /// IP address from which the login challenge was created.
        /// </summary>
        public string? IpAddress { get; set; }

        /// <summary>
        /// User agent string from which the login challenge was created.
        /// </summary>
        public string? UserAgent { get; set; }
    }
}
