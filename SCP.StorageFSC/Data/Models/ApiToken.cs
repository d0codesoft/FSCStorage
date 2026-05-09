namespace SCP.StorageFSC.Data.Models
{
    /// <summary>
    /// API access token.
    /// </summary>
    public sealed class ApiToken : EntityBase
    {
        /// <summary>
        /// Gets or sets the unique identifier for the user.
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// Tenant identifier.
        /// </summary>
        public Guid? TenantId { get; set; }

        /// <summary>
        /// Human-readable token name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Token hash. The raw token itself should not be stored in the database.
        /// </summary>
        public string TokenHash { get; set; } = string.Empty;

        /// <summary>
        /// Token prefix for display/search.
        /// For example, the first 8 characters.
        /// </summary>
        public string TokenPrefix { get; set; } = string.Empty;

        /// <summary>
        /// Token active flag.
        /// </summary>
        public bool IsActive { get; set; }

        public bool IsAdmin { get; set; }

        /// <summary>
        /// Permission to read files.
        /// </summary>
        public bool CanRead { get; set; }

        /// <summary>
        /// Permission to write files.
        /// </summary>
        public bool CanWrite { get; set; }

        /// <summary>
        /// Permission to delete files.
        /// </summary>
        public bool CanDelete { get; set; }

        /// <summary>
        /// Last usage date in UTC.
        /// </summary>
        public DateTime? LastUsedUtc { get; set; }

        /// <summary>
        /// Token expiration date in UTC.
        /// </summary>
        public DateTime? ExpiresUtc { get; set; }

        /// <summary>
        /// Token revocation date in UTC.
        /// </summary>
        public DateTime? RevokedUtc { get; set; }
    }
}
