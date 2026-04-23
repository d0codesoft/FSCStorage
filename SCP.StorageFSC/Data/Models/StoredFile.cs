namespace SCP.StorageFSC.Data.Models
{
    /// <summary>
    /// Physically stored file.
    /// The same file can be used by multiple tenants.
    /// </summary>
    public sealed class StoredFile : EntityBase
    {
        /// <summary>
        /// SHA-256 of the file content.
        /// Primary fingerprint for deduplication.
        /// </summary>
        public string Sha256 { get; set; } = string.Empty;

        /// <summary>
        /// CRC32 of the file content.
        /// Used as a fast additional index/filter.
        /// </summary>
        public string Crc32 { get; set; } = string.Empty;

        /// <summary>
        /// File size in bytes.
        /// </summary>
        public long FileSize { get; set; }

        /// <summary>
        /// Absolute or relative physical file path.
        /// </summary>
        public string PhysicalPath { get; set; } = string.Empty;

        /// <summary>
        /// Original file name, if it needs to be preserved.
        /// </summary>
        public string OriginalFileName { get; set; } = string.Empty;

        /// <summary>
        /// File MIME type.
        /// </summary>
        public string? ContentType { get; set; }

        /// <summary>
        /// Number of active references to the file.
        /// If 0, the file can be physically deleted.
        /// </summary>
        public int ReferenceCount { get; set; }

        /// <summary>
        /// Physical file deletion date in UTC.
        /// </summary>
        public DateTime? DeletedUtc { get; set; }

        /// <summary>
        /// Logical deletion flag.
        /// </summary>
        public bool IsDeleted { get; set; }
    }
}
