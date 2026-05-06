using SCP.StorageFSC.Data.Models;

namespace scp.filestorage.Data.Models
{
    /// <summary>
    /// Represents a temporary two-factor authentication challenge for email or SMS verification.
    /// </summary>
    public sealed class UserTwoFactorChallenge : EntityBase
    {
        /// <summary>
        /// Identifier of the user who owns this two-factor authentication challenge.
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// Identifier of the two-factor authentication method used for this challenge.
        /// </summary>
        public Guid? UserTwoFactorMethodId { get; set; }

        /// <summary>
        /// Two-factor authentication method used for this challenge.
        /// </summary>
        public TwoFactorMethodType MethodType { get; set; }

        /// <summary>
        /// Hash of the one-time verification code. Plain text codes must never be stored.
        /// </summary>
        public string CodeHash { get; set; } = string.Empty;

        /// <summary>
        /// Destination where the verification code was sent, such as email address or phone number.
        /// </summary>
        public string Destination { get; set; } = string.Empty;

        /// <summary>
        /// Current status of the two-factor authentication challenge.
        /// </summary>
        public TwoFactorChallengeStatus Status { get; set; } = TwoFactorChallengeStatus.Pending;

        /// <summary>
        /// UTC date and time when the challenge expires.
        /// </summary>
        public DateTime ExpiresUtc { get; set; }

        /// <summary>
        /// UTC date and time when the challenge was successfully verified.
        /// </summary>
        public DateTime? VerifiedUtc { get; set; }

        /// <summary>
        /// Number of failed verification attempts for this challenge.
        /// </summary>
        public int FailedAttemptCount { get; set; }

        /// <summary>
        /// Maximum allowed failed verification attempts before the challenge is blocked.
        /// </summary>
        public int MaxFailedAttemptCount { get; set; } = 5;

        /// <summary>
        /// IP address from which the challenge was created.
        /// </summary>
        public string? CreatedIpAddress { get; set; }

        /// <summary>
        /// IP address from which the challenge was verified.
        /// </summary>
        public string? VerifiedIpAddress { get; set; }

        /// <summary>
        /// User agent string from which the challenge was created.
        /// </summary>
        public string? UserAgent { get; set; }
    }
}
