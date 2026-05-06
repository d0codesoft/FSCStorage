using SCP.StorageFSC.Data.Models;

namespace scp.filestorage.Data.Models
{
    /// <summary>
    /// Represents a single two-factor authentication recovery code.
    /// </summary>
    public sealed class UserRecoveryCode : EntityBase
    {
        /// <summary>
        /// Identifier of the user who owns this recovery code.
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// Hash of the recovery code. Plain text recovery codes must never be stored.
        /// </summary>
        public string CodeHash { get; set; } = string.Empty;

        /// <summary>
        /// Indicates whether the recovery code has already been used.
        /// </summary>
        public bool IsUsed { get; set; }

        /// <summary>
        /// UTC date and time when the recovery code was used.
        /// </summary>
        public DateTime? UsedUtc { get; set; }

        /// <summary>
        /// IP address from which the recovery code was used.
        /// </summary>
        public string? UsedIpAddress { get; set; }
    }
}
