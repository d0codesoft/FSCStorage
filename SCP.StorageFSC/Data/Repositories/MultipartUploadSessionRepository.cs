using Dapper;
using Microsoft.Data.Sqlite;
using scp.filestorage.Data.Models;
using SCP.StorageFSC.Data;
using System.Data;
using static Dapper.SqlMapper;

namespace scp.filestorage.Data.Repositories
{
    public sealed class MultipartUploadSessionRepository : IMultipartUploadSessionRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public MultipartUploadSessionRepository(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public async Task<Guid> InsertAsync(
            MultipartUploadSession session,
            CancellationToken cancellationToken = default)
        {
            
            ArgumentNullException.ThrowIfNull(session);

            const string sql = """
                INSERT INTO multipart_upload_sessions
                (
                    id,
                    created_utc,
                    updated_utc,
                    row_version,
                    upload_id,
                    tenant_id,
                    original_file_name,
                    normalized_file_name,
                    extension,
                    content_type,
                    total_file_size,
                    part_size,
                    total_parts,
                    expected_checksum_sha256,
                    final_checksum_sha256,
                    status,
                    error_code,
                    error_message,
                    failed_at_utc,
                    storage_provider,
                    temp_storage_bucket,
                    temp_storage_prefix,
                    completed_at_utc,
                    expires_at_utc,
                    stored_file_id
                )
                VALUES
                (
                    @Id,
                    @CreatedUtc,
                    @UpdatedUtc,
                    @RowVersion,
                    @UploadId,
                    @TenantId,
                    @OriginalFileName,
                    @NormalizedFileName,
                    @Extension,
                    @ContentType,
                    @TotalFileSize,
                    @PartSize,
                    @TotalParts,
                    @ExpectedChecksumSha256,
                    @FinalChecksumSha256,
                    @Status,
                    @ErrorCode,
                    @ErrorMessage,
                    @FailedAtUtc,
                    @StorageProvider,
                    @TempStorageBucket,
                    @TempStoragePrefix,
                    @CompletedAtUtc,
                    @ExpiresAtUtc,
                    @StoredFileId
                );
                """;

            try
            {
                using var connection = _connectionFactory.CreateConnection();

                await connection.ExecuteAsync(new CommandDefinition(
                    sql,
                    new
                    {
                        session.Id,
                        session.CreatedUtc,
                        session.UpdatedUtc,
                        session.RowVersion,
                        UploadId = session.UploadId,
                        TenantId = session.TenantId,
                        session.OriginalFileName,
                        session.NormalizedFileName,
                        session.Extension,
                        session.ContentType,
                        session.TotalFileSize,
                        session.PartSize,
                        session.TotalParts,
                        session.ExpectedChecksumSha256,
                        session.FinalChecksumSha256,
                        Status = (short)session.Status,
                        session.ErrorCode,
                        session.ErrorMessage,
                        session.FailedAtUtc,
                        session.StorageProvider,
                        session.TempStorageBucket,
                        session.TempStoragePrefix,
                        session.CompletedAtUtc,
                        session.ExpiresAtUtc,
                        session.StoredFileId
                    },
                    cancellationToken: cancellationToken));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (DataException ex)
            {
                throw new RepositoryException($"Failed to insert multipart upload session '{session.Id}' due to data mapping error.", ex);
            }
            catch (SqliteException ex)
            {
                throw new RepositoryException($"Failed to insert multipart upload session '{session.Id}' due to database error.", ex);
            }

            return session.Id;
        }

        public async Task<MultipartUploadSession?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            const string sql = SelectBaseSql + """
                
                WHERE id = @Id
                LIMIT 1;
                """;

            try
            {
                using var connection = _connectionFactory.CreateConnection();

                return await connection.QuerySingleOrDefaultAsync<MultipartUploadSession>(new CommandDefinition(
                    sql,
                    new { Id = id },
                    cancellationToken: cancellationToken));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (DataException ex)
            {
                throw new RepositoryException($"Failed to load multipart upload session by id '{id}' due to data mapping error.", ex);
            }
            catch (SqliteException ex)
            {
                throw new RepositoryException($"Failed to load multipart upload session by id '{id}' due to database error.", ex);
            }
        }

        public async Task<MultipartUploadSession?> GetByUploadIdAsync(
            Guid uploadId,
            CancellationToken cancellationToken = default)
        {
            const string sql = SelectBaseSql + """
                
                WHERE upload_id = @UploadId
                LIMIT 1;
                """;

            try
            {
                using var connection = _connectionFactory.CreateConnection();

                return await connection.QuerySingleOrDefaultAsync<MultipartUploadSession>(new CommandDefinition(
                    sql,
                    new { UploadId = uploadId },
                    cancellationToken: cancellationToken));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (DataException ex)
            {
                throw new RepositoryException($"Failed to load multipart upload session by upload id '{uploadId}' due to data mapping error.", ex);
            }
            catch (SqliteException ex)
            {
                throw new RepositoryException($"Failed to load multipart upload session by upload id '{uploadId}' due to database error.", ex);
            }
        }

        public async Task<IReadOnlyList<MultipartUploadSession>> GetByTenantIdAsync(
            Guid tenantId,
            CancellationToken cancellationToken = default)
        {
            const string sql = SelectBaseSql + """
                
                WHERE tenant_id = @TenantId
                ORDER BY id DESC;
                """;

            try
            {
                using var connection = _connectionFactory.CreateConnection();

                var rows = await connection.QueryAsync<MultipartUploadSession>(new CommandDefinition(
                    sql,
                    new { TenantId = tenantId },
                    cancellationToken: cancellationToken));

                return rows.ToList();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (DataException ex)
            {
                throw new RepositoryException($"Failed to load multipart upload sessions for tenant '{tenantId}' due to data mapping error.", ex);
            }
            catch (SqliteException ex)
            {
                throw new RepositoryException($"Failed to load multipart upload sessions for tenant '{tenantId}' due to database error.", ex);
            }
        }

        public async Task<IReadOnlyList<MultipartUploadSession>> GetExpiredPendingAsync(
            DateTime utcNow,
            CancellationToken cancellationToken = default)
        {
            const string sql = SelectBaseSql + """
                
                WHERE expires_at_utc IS NOT NULL
                  AND expires_at_utc <= @UtcNow
                  AND status IN (@Created, @Uploading, @Completing)
                ORDER BY expires_at_utc;
                """;

            try
            {
                using var connection = _connectionFactory.CreateConnection();

                var rows = await connection.QueryAsync<MultipartUploadSession>(new CommandDefinition(
                    sql,
                    new
                    {
                        UtcNow = utcNow,
                        Created = (short)MultipartUploadStatus.Created,
                        Uploading = (short)MultipartUploadStatus.Uploading,
                        Completing = (short)MultipartUploadStatus.Completing
                    },
                    cancellationToken: cancellationToken));

                return rows.ToList();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (DataException ex)
            {
                throw new RepositoryException("Failed to load expired multipart upload sessions due to data mapping error.", ex);
            }
            catch (SqliteException ex)
            {
                throw new RepositoryException("Failed to load expired multipart upload sessions due to database error.", ex);
            }
        }

        public async Task<IReadOnlyList<MultipartUploadSession>> GetByStatusAsync(
            MultipartUploadStatus status,
            CancellationToken cancellationToken = default)
        {
            const string sql = """
                SELECT
                    id AS Id,
                    upload_id AS UploadId,
                    tenant_id AS TenantId,
                    original_file_name AS OriginalFileName,
                    normalized_file_name AS NormalizedFileName,
                    extension AS Extension,
                    content_type AS ContentType,
                    total_file_size AS TotalFileSize,
                    part_size AS PartSize,
                    total_parts AS TotalParts,
                    expected_checksum_sha256 AS ExpectedChecksumSha256,
                    final_checksum_sha256 AS FinalChecksumSha256,
                    status AS Status,
                    error_code AS ErrorCode,
                    error_message AS ErrorMessage,
                    failed_at_utc AS FailedAtUtc,
                    storage_provider AS StorageProvider,
                    temp_storage_bucket AS TempStorageBucket,
                    temp_storage_prefix AS TempStoragePrefix,
                    completed_at_utc AS CompletedAtUtc,
                    expires_at_utc AS ExpiresAtUtc,
                    stored_file_id AS StoredFileId,
                    created_utc AS CreatedUtc,
                    updated_utc AS UpdatedUtc,
                    row_version AS RowVersion
                FROM multipart_upload_sessions
                WHERE status = @Status;
                """;

            try
            {
                using var connection = _connectionFactory.CreateConnection();

                var rows = await connection.QueryAsync<MultipartUploadSession>(new CommandDefinition(
                    sql,
                    new { Status = (short)status },
                    cancellationToken: cancellationToken));

                return rows.ToList();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (DataException ex)
            {
                throw new RepositoryException($"Failed to load multipart upload sessions by status '{status}' due to data mapping error.", ex);
            }
            catch (SqliteException ex)
            {
                throw new RepositoryException($"Failed to load multipart upload sessions by status '{status}' due to database error.", ex);
            }
        }

        public async Task<bool> UpdateAsync(
            MultipartUploadSession session,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(session);

            const string sql = """
                UPDATE multipart_upload_sessions
                SET
                    updated_utc = @UpdatedUtc,
                    row_version = @RowVersion,
                    upload_id = @UploadId,
                    tenant_id = @TenantId,
                    original_file_name = @OriginalFileName,
                    normalized_file_name = @NormalizedFileName,
                    extension = @Extension,
                    content_type = @ContentType,
                    total_file_size = @TotalFileSize,
                    part_size = @PartSize,
                    total_parts = @TotalParts,
                    expected_checksum_sha256 = @ExpectedChecksumSha256,
                    final_checksum_sha256 = @FinalChecksumSha256,
                    status = @Status,
                    error_code = @ErrorCode,
                    error_message = @ErrorMessage,
                    failed_at_utc = @FailedAtUtc,
                    storage_provider = @StorageProvider,
                    temp_storage_bucket = @TempStorageBucket,
                    temp_storage_prefix = @TempStoragePrefix,
                    completed_at_utc = @CompletedAtUtc,
                    expires_at_utc = @ExpiresAtUtc,
                    stored_file_id = @StoredFileId
                WHERE id = @Id;
                """;

            try
            {
                using var connection = _connectionFactory.CreateConnection();

                var affected = await connection.ExecuteAsync(new CommandDefinition(
                    sql,
                    new
                    {
                        session.Id,
                        session.UpdatedUtc,
                        session.RowVersion,
                        session.UploadId,
                        session.TenantId,
                        session.OriginalFileName,
                        session.NormalizedFileName,
                        session.Extension,
                        session.ContentType,
                        session.TotalFileSize,
                        session.PartSize,
                        session.TotalParts,
                        session.ExpectedChecksumSha256,
                        session.FinalChecksumSha256,
                        Status = (short)session.Status,
                        session.ErrorCode,
                        session.ErrorMessage,
                        session.FailedAtUtc,
                        session.StorageProvider,
                        session.TempStorageBucket,
                        session.TempStoragePrefix,
                        session.CompletedAtUtc,
                        session.ExpiresAtUtc,
                        session.StoredFileId
                    },
                    cancellationToken: cancellationToken));

                return affected > 0;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (DataException ex)
            {
                throw new RepositoryException($"Failed to update multipart upload session '{session.Id}' due to data mapping error.", ex);
            }
            catch (SqliteException ex)
            {
                throw new RepositoryException($"Failed to update multipart upload session '{session.Id}' due to database error.", ex);
            }
        }

        public async Task<bool> UpdateStatusAsync(
            Guid id,
            MultipartUploadStatus status,
            string? errorCode = null,
            string? errorMessage = null,
            DateTime? failedAtUtc = null,
            DateTime? completedAtUtc = null,
            Guid? storedFileId = null,
            CancellationToken cancellationToken = default)
        {
            const string sql = """
                UPDATE multipart_upload_sessions
                SET
                    status = @Status,
                    error_code = @ErrorCode,
                    error_message = @ErrorMessage,
                    failed_at_utc = @FailedAtUtc,
                    completed_at_utc = @CompletedAtUtc,
                    stored_file_id = @StoredFileId,
                    updated_utc = @UpdatedUtc
                WHERE id = @Id;
                """;

            try
            {
                using var connection = _connectionFactory.CreateConnection();

                var affected = await connection.ExecuteAsync(new CommandDefinition(
                    sql,
                    new
                    {
                        Id = id,
                        Status = (short)status,
                        ErrorCode = errorCode,
                        ErrorMessage = errorMessage,
                        FailedAtUtc = failedAtUtc,
                        CompletedAtUtc = completedAtUtc,
                        StoredFileId = storedFileId,
                        UpdatedUtc = DateTime.UtcNow
                    },
                    cancellationToken: cancellationToken));

                return affected > 0;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (DataException ex)
            {
                throw new RepositoryException($"Failed to update status for multipart upload session '{id}' due to data mapping error.", ex);
            }
            catch (SqliteException ex)
            {
                throw new RepositoryException($"Failed to update status for multipart upload session '{id}' due to database error.", ex);
            }
        }

        public async Task<bool> TouchUpdatedAsync(
            Guid id,
            DateTime updatedUtc,
            CancellationToken cancellationToken = default)
        {
            const string sql = """
                UPDATE multipart_upload_sessions
                SET updated_utc = @UpdatedUtc
                WHERE id = @Id;
                """;

            try
            {
                using var connection = _connectionFactory.CreateConnection();

                var affected = await connection.ExecuteAsync(new CommandDefinition(
                    sql,
                    new
                    {
                        Id = id,
                        UpdatedUtc = updatedUtc
                    },
                    cancellationToken: cancellationToken));

                return affected > 0;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (DataException ex)
            {
                throw new RepositoryException($"Failed to touch multipart upload session '{id}' due to data mapping error.", ex);
            }
            catch (SqliteException ex)
            {
                throw new RepositoryException($"Failed to touch multipart upload session '{id}' due to database error.", ex);
            }
        }

        public async Task<bool> DeleteAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            const string sql = """
                DELETE FROM multipart_upload_sessions
                WHERE id = @Id;
                """;

            try
            {
                using var connection = _connectionFactory.CreateConnection();

                var affected = await connection.ExecuteAsync(new CommandDefinition(
                    sql,
                    new { Id = id },
                    cancellationToken: cancellationToken));

                return affected > 0;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (DataException ex)
            {
                throw new RepositoryException($"Failed to delete multipart upload session '{id}' due to data mapping error.", ex);
            }
            catch (SqliteException ex)
            {
                throw new RepositoryException($"Failed to delete multipart upload session '{id}' due to database error.", ex);
            }
        }

        public async Task<int> DeleteTerminalOlderThanAsync(
            DateTime cutoffUtc,
            CancellationToken cancellationToken = default)
        {
            const string sql = """
                DELETE FROM multipart_upload_sessions
                WHERE status IN (@Completed, @Aborted, @Failed, @Expired)
                  AND COALESCE(completed_at_utc, failed_at_utc, updated_utc, created_utc) < @CutoffUtc;
                """;

            try
            {
                using var connection = _connectionFactory.CreateConnection();

                return await connection.ExecuteAsync(new CommandDefinition(
                    sql,
                    new
                    {
                        CutoffUtc = cutoffUtc,
                        Completed = (short)MultipartUploadStatus.Completed,
                        Aborted = (short)MultipartUploadStatus.Aborted,
                        Failed = (short)MultipartUploadStatus.Failed,
                        Expired = (short)MultipartUploadStatus.Expired
                    },
                    cancellationToken: cancellationToken));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (DataException ex)
            {
                throw new RepositoryException("Failed to delete old multipart upload sessions due to data mapping error.", ex);
            }
            catch (SqliteException ex)
            {
                throw new RepositoryException("Failed to delete old multipart upload sessions due to database error.", ex);
            }
        }

        private const string SelectBaseSql = """
            SELECT
                id AS Id,
                created_utc AS CreatedUtc,
                updated_utc AS UpdatedUtc,
                row_version AS RowVersion,
                upload_id AS UploadId,
                tenant_id AS TenantId,
                original_file_name AS OriginalFileName,
                normalized_file_name AS NormalizedFileName,
                extension AS Extension,
                content_type AS ContentType,
                total_file_size AS TotalFileSize,
                part_size AS PartSize,
                total_parts AS TotalParts,
                expected_checksum_sha256 AS ExpectedChecksumSha256,
                final_checksum_sha256 AS FinalChecksumSha256,
                status AS Status,
                error_code AS ErrorCode,
                error_message AS ErrorMessage,
                failed_at_utc AS FailedAtUtc,
                storage_provider AS StorageProvider,
                temp_storage_bucket AS TempStorageBucket,
                temp_storage_prefix AS TempStoragePrefix,
                completed_at_utc AS CompletedAtUtc,
                expires_at_utc AS ExpiresAtUtc,
                stored_file_id AS StoredFileId
            FROM multipart_upload_sessions
            """;
    }
}
