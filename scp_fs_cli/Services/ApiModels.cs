using scp_fs_cli.Infrastructure;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace scp_fs_cli.Services
{
    public sealed record InitMultipartUploadRequest(
        [property: JsonPropertyName("fileName")] string FileName,
        [property: JsonPropertyName("fileSize")] long FileSize,
        [property: JsonPropertyName("contentType")] string ContentType,
        [property: JsonPropertyName("partSize")] long PartSize,
        [property: JsonPropertyName("expectedChecksumSha256")] string? ExpectedChecksumSha256,
        [property: JsonPropertyName("tenantId")] Guid TenantId,
        [property: JsonPropertyName("metadata")] Dictionary<string, string>? Metadata,
        [property: JsonPropertyName("expiresAtUtc")] DateTimeOffset? ExpiresAtUtc);

    public sealed record InitMultipartUploadResult(
        [property: JsonPropertyName("uploadId")] Guid UploadId,
        [property: JsonPropertyName("tenantId")] Guid TenantId,
        [property: JsonPropertyName("fileName")] string FileName,
        [property: JsonPropertyName("fileSize")] long FileSize,
        [property: JsonPropertyName("partSize")] long PartSize,
        [property: JsonPropertyName("totalParts")] int TotalParts,
        [property: JsonPropertyName("status")] MultipartUploadStatus Status,
        [property: JsonPropertyName("createdAtUtc")] DateTime CreatedAtUtc,
        [property: JsonPropertyName("expiresAtUtc")] DateTime? ExpiresAtUtc);

    public sealed record CompleteMultipartUploadRequest(
        [property: JsonPropertyName("uploadId")] Guid UploadId);

    public sealed record CompleteMultipartUploadResult(
        [property: JsonPropertyName("uploadId")] Guid UploadId,
        [property: JsonPropertyName("tenantId")] Guid TenantId,
        [property: JsonPropertyName("fileName")] string FileName,
        [property: JsonPropertyName("fileSize")] long FileSize,
        [property: JsonPropertyName("contentType")] string? ContentType,
        [property: JsonPropertyName("finalChecksumSha256")] string? FinalChecksumSha256,
        [property: JsonPropertyName("physicalPath")] string PhysicalPath,
        [property: JsonPropertyName("relativePath")] string RelativePath,
        [property: JsonPropertyName("completedAtUtc")] DateTime CompletedAtUtc,
        [property: JsonPropertyName("status")] MultipartUploadStatus Status,
        [property: JsonPropertyName("storedFileId")] Guid? StoredFileId);

    public sealed record MultipartUploadStatusResult(
        [property: JsonPropertyName("uploadId")] Guid UploadId,
        [property: JsonPropertyName("tenantId")] Guid TenantId,
        [property: JsonPropertyName("fileName")] string FileName,
        [property: JsonPropertyName("normalizedFileName")] string NormalizedFileName,
        [property: JsonPropertyName("extension")] string Extension,
        [property: JsonPropertyName("contentType")] string? ContentType,
        [property: JsonPropertyName("totalFileSize")] long TotalFileSize,
        [property: JsonPropertyName("partSize")] long PartSize,
        [property: JsonPropertyName("totalParts")] int TotalParts,
        [property: JsonPropertyName("uploadedPartCount")] int UploadedPartCount,
        [property: JsonPropertyName("uploadedParts")] IReadOnlyList<int> UploadedParts,
        [property: JsonPropertyName("status")] MultipartUploadStatus Status,
        [property: JsonPropertyName("finalChecksumSha256")] string? FinalChecksumSha256,
        [property: JsonPropertyName("relativePath")] string RelativePath,
        [property: JsonPropertyName("physicalPath")] string PhysicalPath,
        [property: JsonPropertyName("errorCode")] string? ErrorCode,
        [property: JsonPropertyName("errorMessage")] string? ErrorMessage,
        [property: JsonPropertyName("tempStoragePrefix")] string TempStoragePrefix,
        [property: JsonPropertyName("createdAtUtc")] DateTime CreatedAtUtc,
        [property: JsonPropertyName("updatedAtUtc")] DateTime? UpdatedAtUtc,
        [property: JsonPropertyName("completedAtUtc")] DateTime? CompletedAtUtc,
        [property: JsonPropertyName("expiresAtUtc")] DateTime? ExpiresAtUtc,
        [property: JsonPropertyName("storedFileId")] Guid? StoredFileId);

    public sealed record UploadMultipartPartResult(
        [property: JsonPropertyName("uploadId")] Guid UploadId,
        [property: JsonPropertyName("partNumber")] int PartNumber,
        [property: JsonPropertyName("offsetBytes")] long OffsetBytes,
        [property: JsonPropertyName("sizeInBytes")] long SizeInBytes,
        [property: JsonPropertyName("storageKey")] string StorageKey,
        [property: JsonPropertyName("checksumSha256")] string? ChecksumSha256,
        [property: JsonPropertyName("status")] MultipartUploadPartStatus Status,
        [property: JsonPropertyName("uploadedAtUtc")] DateTime? UploadedAtUtc);

    public sealed record AbortMultipartUploadResult(
        [property: JsonPropertyName("uploadId")] Guid UploadId,
        [property: JsonPropertyName("status")] MultipartUploadStatus Status,
        [property: JsonPropertyName("updatedAtUtc")] DateTime UpdatedAtUtc);

    public enum MultipartUploadStatus
    {
        Created = 0,
        Uploading = 1,
        Completing = 2,
        Completed = 3,
        Aborted = 4,
        Failed = 5,
        Expired = 6
    }

    public enum MultipartUploadPartStatus
    {
        Pending = 0,
        Uploaded = 1,
        Verified = 2,
        Failed = 3
    }

    public sealed record SaveFileResult(
        [property: JsonPropertyName("success")] bool Success,
        [property: JsonPropertyName("status")] SaveFileStatus Status,
        [property: JsonPropertyName("errorCode")] string? ErrorCode,
        [property: JsonPropertyName("errorMessage")] string? ErrorMessage,
        [property: JsonPropertyName("file")] StoredTenantFileResult? File,
        [property: JsonPropertyName("isDeduplicated")] bool IsDeduplicated,
        [property: JsonPropertyName("alreadyExistsForTenant")] bool AlreadyExistsForTenant);

    public enum SaveFileStatus
    {
        Success = 0,
        ValidationError = 1,
        AccessDenied = 2,
        StorageFailed = 3,
        DatabaseFailed = 4,
        DuplicateFile = 5,
        AlreadyExists = 6
    }

    public sealed record StoredTenantFileResult(
        [property: JsonPropertyName("tenantFileId")] Guid TenantFileId,
        [property: JsonPropertyName("fileGuid")] Guid FileGuid,
        [property: JsonPropertyName("tenantId")] Guid TenantId,
        [property: JsonPropertyName("storedFileId")] Guid StoredFileId,
        [property: JsonPropertyName("fileName")] string FileName,
        [property: JsonPropertyName("category")] string? Category,
        [property: JsonPropertyName("externalKey")] string? ExternalKey,
        [property: JsonPropertyName("contentType")] string? ContentType,
        [property: JsonPropertyName("filestoreStateCompress")] FilestoreStateCompress FilestoreStateCompress,
        [property: JsonPropertyName("fileSize")] long FileSize,
        [property: JsonPropertyName("sha256")] string Sha256,
        [property: JsonPropertyName("crc32")] string Crc32,
        [property: JsonPropertyName("createdUtc")] DateTime CreatedUtc);

    public enum FilestoreStateCompress
    {
        NoCompressionNeeded = 0,
        CanBeCompressed = 1,
        Compressed = 2
    }

    public static class ApiModelDisplayExtensions
    {
        public static string ToDisplayJson<T>(this T value)
        {
            return JsonSerializer.Serialize(value, JsonOptions.Pretty);
        }
    }
}
