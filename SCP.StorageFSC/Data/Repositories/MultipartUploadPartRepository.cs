using Dapper;
using scp.filestorage.Data.Models;
using SCP.StorageFSC.Data;

namespace scp.filestorage.Data.Repositories
{
    public sealed class MultipartUploadPartRepository : IMultipartUploadPartRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public MultipartUploadPartRepository(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public async Task<Guid> InsertAsync(
            MultipartUploadPart part,
            CancellationToken cancellationToken = default)
        {
            const string sql = """
                INSERT INTO multipart_upload_parts
                (
                    id,
                    public_id,
                    created_utc,
                    updated_utc,
                    row_version,
                    multipart_upload_session_id,
                    part_number,
                    offset_bytes,
                    size_in_bytes,
                    storage_key,
                    checksum_sha256,
                    provider_part_etag,
                    status,
                    uploaded_at_utc,
                    error_message,
                    retry_count,
                    last_failed_at_utc
                )
                VALUES
                (
                    @Id,
                    @PublicId,
                    @CreatedUtc,
                    @UpdatedUtc,
                    @RowVersion,
                    @MultipartUploadSessionId,
                    @PartNumber,
                    @OffsetBytes,
                    @SizeInBytes,
                    @StorageKey,
                    @ChecksumSha256,
                    @ProviderPartETag,
                    @Status,
                    @UploadedAtUtc,
                    @ErrorMessage,
                    @RetryCount,
                    @LastFailedAtUtc
                );
                """;

            using var connection = _connectionFactory.CreateConnection();

            await connection.ExecuteAsync(new CommandDefinition(
                sql,
                new
                {
                    part.Id,
                    part.PublicId,
                    part.CreatedUtc,
                    part.UpdatedUtc,
                    part.RowVersion,
                    part.MultipartUploadSessionId,
                    part.PartNumber,
                    part.OffsetBytes,
                    part.SizeInBytes,
                    part.StorageKey,
                    part.ChecksumSha256,
                    part.ProviderPartETag,
                    Status = (short)part.Status,
                    part.UploadedAtUtc,
                    part.ErrorMessage,
                    part.RetryCount,
                    part.LastFailedAtUtc
                },
                cancellationToken: cancellationToken));

            return part.Id;
        }

        public async Task<MultipartUploadPart?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            const string sql = SelectBaseSql + """
                WHERE id = @Id
                LIMIT 1;
                """;

            using var connection = _connectionFactory.CreateConnection();

            return await connection.QuerySingleOrDefaultAsync<MultipartUploadPart>(new CommandDefinition(
                sql,
                new { Id = id },
                cancellationToken: cancellationToken));
        }

        public async Task<MultipartUploadPart?> GetByPublicIdAsync(
            Guid publicId,
            CancellationToken cancellationToken = default)
        {
            const string sql = SelectBaseSql + """
                WHERE public_id = @PublicId
                LIMIT 1;
                """;

            using var connection = _connectionFactory.CreateConnection();

            return await connection.QuerySingleOrDefaultAsync<MultipartUploadPart>(new CommandDefinition(
                sql,
                new { PublicId = publicId.ToString() },
                cancellationToken: cancellationToken));
        }

        public async Task<MultipartUploadPart?> GetBySessionAndPartNumberAsync(
            Guid multipartUploadSessionId,
            int partNumber,
            CancellationToken cancellationToken = default)
        {
            const string sql = SelectBaseSql + """
                WHERE multipart_upload_session_id = @MultipartUploadSessionId
                  AND part_number = @PartNumber
                LIMIT 1;
                """;

            using var connection = _connectionFactory.CreateConnection();

            return await connection.QuerySingleOrDefaultAsync<MultipartUploadPart>(new CommandDefinition(
                sql,
                new
                {
                    MultipartUploadSessionId = multipartUploadSessionId,
                    PartNumber = partNumber
                },
                cancellationToken: cancellationToken));
        }

        public async Task<IReadOnlyList<MultipartUploadPart>> GetBySessionIdAsync(
            Guid multipartUploadSessionId,
            CancellationToken cancellationToken = default)
        {
            const string sql = SelectBaseSql + """
                WHERE multipart_upload_session_id = @MultipartUploadSessionId
                ORDER BY part_number;
                """;

            using var connection = _connectionFactory.CreateConnection();

            var rows = await connection.QueryAsync<MultipartUploadPart>(new CommandDefinition(
                sql,
                new { MultipartUploadSessionId = multipartUploadSessionId },
                cancellationToken: cancellationToken));

            return rows.ToList();
        }

        public async Task<int> CountUploadedPartsAsync(
            Guid multipartUploadSessionId,
            CancellationToken cancellationToken = default)
        {
            const string sql = """
                SELECT COUNT(1)
                FROM multipart_upload_parts
                WHERE multipart_upload_session_id = @MultipartUploadSessionId
                  AND status IN (@Uploaded, @Verified);
                """;

            using var connection = _connectionFactory.CreateConnection();

            var count = await connection.ExecuteScalarAsync<long>(new CommandDefinition(
                sql,
                new
                {
                    MultipartUploadSessionId = multipartUploadSessionId,
                    Uploaded = (short)MultipartUploadPartStatus.Uploaded,
                    Verified = (short)MultipartUploadPartStatus.Verified
                },
                cancellationToken: cancellationToken));

            return (int)count;
        }

        public async Task<bool> UpsertAsync(
            MultipartUploadPart part,
            CancellationToken cancellationToken = default)
        {
            const string sql = """
                INSERT INTO multipart_upload_parts
                (
                    public_id,
                    created_utc,
                    updated_utc,
                    row_version,
                    multipart_upload_session_id,
                    part_number,
                    offset_bytes,
                    size_in_bytes,
                    storage_key,
                    checksum_sha256,
                    provider_part_etag,
                    status,
                    uploaded_at_utc,
                    error_message,
                    retry_count,
                    last_failed_at_utc
                )
                VALUES
                (
                    @PublicId,
                    @CreatedUtc,
                    @UpdatedUtc,
                    @RowVersion,
                    @MultipartUploadSessionId,
                    @PartNumber,
                    @OffsetBytes,
                    @SizeInBytes,
                    @StorageKey,
                    @ChecksumSha256,
                    @ProviderPartETag,
                    @Status,
                    @UploadedAtUtc,
                    @ErrorMessage,
                    @RetryCount,
                    @LastFailedAtUtc
                )
                ON CONFLICT(multipart_upload_session_id, part_number)
                DO UPDATE SET
                    updated_utc = excluded.updated_utc,
                    row_version = excluded.row_version,
                    offset_bytes = excluded.offset_bytes,
                    size_in_bytes = excluded.size_in_bytes,
                    storage_key = excluded.storage_key,
                    checksum_sha256 = excluded.checksum_sha256,
                    provider_part_etag = excluded.provider_part_etag,
                    status = excluded.status,
                    uploaded_at_utc = excluded.uploaded_at_utc,
                    error_message = excluded.error_message,
                    retry_count = excluded.retry_count,
                    last_failed_at_utc = excluded.last_failed_at_utc;
                """;

            using var connection = _connectionFactory.CreateConnection();

            var affected = await connection.ExecuteAsync(new CommandDefinition(
                sql,
                new
                {
                    PublicId = part.PublicId.ToString(),
                    part.CreatedUtc,
                    part.UpdatedUtc,
                    part.RowVersion,
                    part.MultipartUploadSessionId,
                    part.PartNumber,
                    part.OffsetBytes,
                    part.SizeInBytes,
                    part.StorageKey,
                    part.ChecksumSha256,
                    part.ProviderPartETag,
                    Status = (short)part.Status,
                    part.UploadedAtUtc,
                    part.ErrorMessage,
                    part.RetryCount,
                    part.LastFailedAtUtc
                },
                cancellationToken: cancellationToken));

            return affected > 0;
        }

        public async Task<bool> UpdateAsync(
            MultipartUploadPart part,
            CancellationToken cancellationToken = default)
        {
            const string sql = """
                UPDATE multipart_upload_parts
                SET
                    updated_utc = @UpdatedUtc,
                    row_version = @RowVersion,
                    multipart_upload_session_id = @MultipartUploadSessionId,
                    part_number = @PartNumber,
                    offset_bytes = @OffsetBytes,
                    size_in_bytes = @SizeInBytes,
                    storage_key = @StorageKey,
                    checksum_sha256 = @ChecksumSha256,
                    provider_part_etag = @ProviderPartETag,
                    status = @Status,
                    uploaded_at_utc = @UploadedAtUtc,
                    error_message = @ErrorMessage,
                    retry_count = @RetryCount,
                    last_failed_at_utc = @LastFailedAtUtc
                WHERE id = @Id;
                """;

            using var connection = _connectionFactory.CreateConnection();

            var affected = await connection.ExecuteAsync(new CommandDefinition(
                sql,
                new
                {
                    part.Id,
                    part.UpdatedUtc,
                    part.RowVersion,
                    part.MultipartUploadSessionId,
                    part.PartNumber,
                    part.OffsetBytes,
                    part.SizeInBytes,
                    part.StorageKey,
                    part.ChecksumSha256,
                    part.ProviderPartETag,
                    Status = (short)part.Status,
                    part.UploadedAtUtc,
                    part.ErrorMessage,
                    part.RetryCount,
                    part.LastFailedAtUtc
                },
                cancellationToken: cancellationToken));

            return affected > 0;
        }

        public async Task<bool> UpdateStatusAsync(
            Guid id,
            MultipartUploadPartStatus status,
            DateTime? uploadedAtUtc = null,
            string? errorMessage = null,
            int? retryCount = null,
            DateTime? lastFailedAtUtc = null,
            string? checksumSha256 = null,
            string? providerPartETag = null,
            CancellationToken cancellationToken = default)
        {
            const string sql = """
                UPDATE multipart_upload_parts
                SET
                    status = @Status,
                    uploaded_at_utc = @UploadedAtUtc,
                    error_message = @ErrorMessage,
                    retry_count = COALESCE(@RetryCount, retry_count),
                    last_failed_at_utc = @LastFailedAtUtc,
                    checksum_sha256 = COALESCE(@ChecksumSha256, checksum_sha256),
                    provider_part_etag = COALESCE(@ProviderPartETag, provider_part_etag),
                    updated_utc = @UpdatedUtc
                WHERE id = @Id;
                """;

            using var connection = _connectionFactory.CreateConnection();

            var affected = await connection.ExecuteAsync(new CommandDefinition(
                sql,
                new
                {
                    Id = id,
                    Status = (short)status,
                    UploadedAtUtc = uploadedAtUtc,
                    ErrorMessage = errorMessage,
                    RetryCount = retryCount,
                    LastFailedAtUtc = lastFailedAtUtc,
                    ChecksumSha256 = checksumSha256,
                    ProviderPartETag = providerPartETag,
                    UpdatedUtc = DateTime.UtcNow
                },
                cancellationToken: cancellationToken));

            return affected > 0;
        }

        public async Task<bool> DeleteBySessionIdAsync(
            Guid multipartUploadSessionId,
            CancellationToken cancellationToken = default)
        {
            const string sql = """
                DELETE FROM multipart_upload_parts
                WHERE multipart_upload_session_id = @MultipartUploadSessionId;
                """;

            using var connection = _connectionFactory.CreateConnection();

            var affected = await connection.ExecuteAsync(new CommandDefinition(
                sql,
                new { MultipartUploadSessionId = multipartUploadSessionId },
                cancellationToken: cancellationToken));

            return affected > 0;
        }

        public async Task<bool> DeleteAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            const string sql = """
                DELETE FROM multipart_upload_parts
                WHERE id = @Id;
                """;

            using var connection = _connectionFactory.CreateConnection();

            var affected = await connection.ExecuteAsync(new CommandDefinition(
                sql,
                new { Id = id },
                cancellationToken: cancellationToken));

            return affected > 0;
        }

        private const string SelectBaseSql = """
            SELECT
                id AS Id,
                public_id AS PublicId,
                created_utc AS CreatedUtc,
                updated_utc AS UpdatedUtc,
                row_version AS RowVersion,
                multipart_upload_session_id AS MultipartUploadSessionId,
                part_number AS PartNumber,
                offset_bytes AS OffsetBytes,
                size_in_bytes AS SizeInBytes,
                storage_key AS StorageKey,
                checksum_sha256 AS ChecksumSha256,
                provider_part_etag AS ProviderPartETag,
                status AS Status,
                uploaded_at_utc AS UploadedAtUtc,
                error_message AS ErrorMessage,
                retry_count AS RetryCount,
                last_failed_at_utc AS LastFailedAtUtc
            FROM multipart_upload_parts
            """;
    }
}
