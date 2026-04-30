using SCP.StorageFSC.Data.Models;

namespace scp.filestorage.Data.Models
{
    /// <summary>
    /// Represents a single part of a multipart upload session.
    /// </summary>
    public sealed class MultipartUploadPart : EntityBase
    {
        /// <summary>
        /// Gets or sets the multipart upload session identifier.
        /// </summary>
        public Guid MultipartUploadSessionId { get; set; }

        /// <summary>
        /// Gets or sets the part number.
        /// </summary>
        public int PartNumber { get; set; }

        /// <summary>
        /// Gets or sets the offset in bytes.
        /// </summary>
        public long OffsetBytes { get; set; }

        /// <summary>
        /// Gets or sets the part size in bytes.
        /// </summary>
        public long SizeInBytes { get; set; }

        /// <summary>
        /// Gets or sets the storage key.
        /// </summary>
        public string StorageKey { get; set; } = string.Empty;        // temp/{tenantId}/{uploadId}/part-000001

        /// <summary>
        /// Gets or sets the SHA-256 checksum.
        /// </summary>
        public string? ChecksumSha256 { get; set; }

        /// <summary>
        /// Gets or sets the provider-specific part ETag.
        /// </summary>
        public string? ProviderPartETag { get; set; }

        /// <summary>
        /// Gets or sets the multipart upload part status.
        /// </summary>
        public MultipartUploadPartStatus Status { get; set; }         // Pending, Uploaded, Verified, Failed

        /// <summary>
        /// Gets or sets the upload date and time in UTC.
        /// </summary>
        public DateTime? UploadedAtUtc { get; set; }

        /// <summary>
        /// Gets or sets the error message.
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Gets or sets the retry count.
        /// </summary>
        public int RetryCount { get; set; }

        /// <summary>
        /// Gets or sets the last failure date and time in UTC.
        /// </summary>
        public DateTime? LastFailedAtUtc { get; set; }
    }
}