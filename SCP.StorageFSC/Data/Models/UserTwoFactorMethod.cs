using SCP.StorageFSC.Data.Models;

namespace scp.filestorage.Data.Models
{
    /// <summary>
    /// Represents a two-factor authentication method configured for a user.
    /// </summary>
    public sealed class UserTwoFactorMethod : EntityBase
    {
        /// <summary>
        /// Identifier of the user who owns this two-factor authentication method.
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// Type of the two-factor authentication method.
        /// </summary>
        public TwoFactorMethodType MethodType { get; set; }

        /// <summary>
        /// Indicates whether this two-factor authentication method is enabled.
        /// </summary>
        public bool IsEnabled { get; set; }

        /// <summary>
        /// Indicates whether this two-factor authentication method has been confirmed by the user.
        /// </summary>
        public bool IsConfirmed { get; set; }

        /// <summary>
        /// Indicates whether this method is the default two-factor authentication method for the user.
        /// </summary>
        public bool IsDefault { get; set; }

        /// <summary>
        /// Encrypted secret value used by the authentication method.
        /// For authenticator applications this is the encrypted TOTP secret.
        /// For email and SMS this field is usually null.
        /// </summary>
        public string? SecretEncrypted { get; set; }

        /// <summary>
        /// Destination used by this method, such as an email address or phone number.
        /// This value may be null for authenticator applications.
        /// </summary>
        public string? Destination { get; set; }

        /// <summary>
        /// Masked destination used for safe display, such as masked email address or masked phone number.
        /// </summary>
        public string? MaskedDestination { get; set; }

        /// <summary>
        /// UTC date and time when this two-factor authentication method was confirmed.
        /// </summary>
        public DateTime? ConfirmedUtc { get; set; }

        /// <summary>
        /// UTC date and time when this two-factor authentication method was last successfully used.
        /// </summary>
        public DateTime? LastUsedUtc { get; set; }

        /// <summary>
        /// Number of consecutive failed verification attempts for this method.
        /// </summary>
        public int FailedAttemptCount { get; set; }

        /// <summary>
        /// UTC date and time of the last failed verification attempt.
        /// </summary>
        public DateTime? LastFailedAttemptUtc { get; set; }

        /// <summary>
        /// UTC date and time until which this method is temporarily locked.
        /// </summary>
        public DateTime? LockedUntilUtc { get; set; }
    }
}
