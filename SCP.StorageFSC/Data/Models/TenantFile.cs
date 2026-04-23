namespace SCP.StorageFSC.Data.Models
{
    /// <summary>
    /// File-to-tenant binding.
    /// This is the logical "tenant file" entity.
    /// </summary>
    public sealed class TenantFile : EntityBase
    {
        /// <summary>
        /// Tenant identifier.
        /// </summary>
        public Guid TenantId { get; set; }

        /// <summary>
        /// Physical file identifier.
        /// </summary>
        public Guid StoredFileId { get; set; }

        /// <summary>
        /// External file GUID for the API.
        /// Convenient to use in URLs and client operations.
        /// </summary>
        public Guid FileGuid { get; set; }

        /// <summary>
        /// Logical file name within the tenant.
        /// </summary>
        public string FileName { get; set; } = string.Empty;

        /// <summary>
        /// Optional category/group.
        /// </summary>
        public string? Category { get; set; }

        /// <summary>
        /// Custom key/document code.
        /// For example, a document number, external ID, and so on.
        /// </summary>
        public string? ExternalKey { get; set; }

        /// <summary>
        /// Binding active flag.
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// Binding deletion date in UTC.
        /// </summary>
        public DateTime? DeletedUtc { get; set; }
    }
}
