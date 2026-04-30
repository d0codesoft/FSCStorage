using SCP.StorageFSC.Data.Models;

namespace scp.filestorage.Data.Models
{
    /// <summary>
    /// Represents a multipart file upload session.
    /// </summary>
    public sealed class MultipartUploadSession : EntityBase
    {
        /// <summary>
        /// Gets or sets the upload identifier.
        /// </summary>
        public Guid UploadId { get; set; }

        /// <summary>
        /// Gets or sets the tenant identifier.
        /// </summary>
        public Guid TenantId { get; set; }

        /// <summary>
        /// Gets or sets the original file name.
        /// </summary>
        public string OriginalFileName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the normalized file name.
        /// </summary>
        public string NormalizedFileName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the file extension.
        /// </summary>
        public string Extension { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the content type.
        /// </summary>
        public string? ContentType { get; set; }

        /// <summary>
        /// Gets or sets the total file size in bytes.
        /// </summary>
        public long TotalFileSize { get; set; }

        /// <summary>
        /// Gets or sets the part size in bytes.
        /// </summary>
        public long PartSize { get; set; }

        /// <summary>
        /// Gets or sets the total number of parts.
        /// </summary>
        public int TotalParts { get; set; }

        /// <summary>
        /// Gets or sets the expected SHA-256 checksum.
        /// </summary>
        public string? ExpectedChecksumSha256 { get; set; }

        /// <summary>
        /// Gets or sets the final SHA-256 checksum.
        /// </summary>
        public string? FinalChecksumSha256 { get; set; }

        /// <summary>
        /// Gets or sets the upload session status.
        /// </summary>
        public MultipartUploadStatus Status { get; set; } // Created, Uploading, Completing, Completed, Aborted, Failed, Expired

        /// <summary>
        /// Gets or sets the error code.
        /// </summary>
        public string? ErrorCode { get; set; }

        /// <summary>
        /// Gets or sets the error message.
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Gets or sets the failure date and time in UTC.
        /// </summary>
        public DateTime? FailedAtUtc { get; set; }

        /// <summary>
        /// Gets or sets the storage provider.
        /// </summary>
        public string StorageProvider { get; set; } = string.Empty;   // Disk, S3, Minio

        /// <summary>
        /// Gets or sets the temporary storage bucket.
        /// </summary>
        public string? TempStorageBucket { get; set; }

        /// <summary>
        /// Gets or sets the temporary storage prefix.
        /// </summary>
        public string TempStoragePrefix { get; set; } = string.Empty; // temp/{tenantId}/{uploadId}/

        /// <summary>
        /// Gets or sets the completion date and time in UTC.
        /// </summary>
        public DateTime? CompletedAtUtc { get; set; }

        /// <summary>
        /// Gets or sets the expiration date and time in UTC.
        /// </summary>
        public DateTime? ExpiresAtUtc { get; set; }

        /// <summary>
        /// Gets or sets the stored file identifier.
        /// </summary>
        public Guid? StoredFileId { get; set; }
    }
}