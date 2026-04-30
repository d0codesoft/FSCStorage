using Dapper;
using Microsoft.Data.Sqlite;
using SCP.StorageFSC.Data.Models;
using System.Data;

namespace SCP.StorageFSC.Data.Repositories
{
    public sealed class StoredFileRepository : IStoredFileRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public StoredFileRepository(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public async Task<Guid> InsertAsync(StoredFile file, CancellationToken cancellationToken = default)
        {
            const string sql = """
                INSERT INTO stored_files
                (
                    id,
                    sha256,
                    crc32,
                    file_size,
                    physical_path,
                    original_file_name,
                    content_type,
                    filestore_state_compress,
                    reference_count,
                    created_utc,
                    updated_utc,
                    row_version,
                    deleted_utc,
                    is_deleted
                )
                VALUES
                (
                    @Id,
                    @Sha256,
                    @Crc32,
                    @FileSize,
                    @PhysicalPath,
                    @OriginalFileName,
                    @ContentType,
                    @FilestoreStateCompress,
                    @ReferenceCount,
                    @CreatedUtc,
                    @UpdatedUtc,
                    @RowVersion,
                    @DeletedUtc,
                    @IsDeleted
                );
                """;

            try
            {
                using var connection = _connectionFactory.CreateConnection();

                await connection.ExecuteAsync(new CommandDefinition(
                    sql,
                    new
                    {
                        file.Id,
                        file.Sha256,
                        file.Crc32,
                        file.FileSize,
                        file.PhysicalPath,
                        file.OriginalFileName,
                        file.ContentType,
                        FilestoreStateCompress = (short)file.FilestoreStateCompress,
                        file.ReferenceCount,
                        file.CreatedUtc,
                        file.UpdatedUtc,
                        file.RowVersion,
                        file.DeletedUtc,
                        IsDeleted = file.IsDeleted ? 1 : 0
                    },
                    cancellationToken: cancellationToken));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (DataException ex)
            {
                throw new RepositoryException($"Failed to insert stored file '{file.Id}' due to data mapping error.", ex);
            }
            catch (SqliteException ex)
            {
                throw new RepositoryException($"Failed to insert stored file '{file.Id}' due to database error.", ex);
            }

            return file.Id;
        }

        public async Task<StoredFile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            const string sql = """
                SELECT
                    id AS Id,
                    sha256 AS Sha256,
                    crc32 AS Crc32,
                    file_size AS FileSize,
                    physical_path AS PhysicalPath,
                    original_file_name AS OriginalFileName,
                    content_type AS ContentType,
                    filestore_state_compress AS FilestoreStateCompress,
                    reference_count AS ReferenceCount,
                    created_utc AS CreatedUtc,
                    updated_utc AS UpdatedUtc,
                    row_version AS RowVersion,
                    deleted_utc AS DeletedUtc,
                    is_deleted AS IsDeleted
                FROM stored_files
                WHERE id = @Id
                LIMIT 1;
                """;

            try
            {
                using var connection = _connectionFactory.CreateConnection();
                return await connection.QuerySingleOrDefaultAsync<StoredFile>(new CommandDefinition(
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
                throw new RepositoryException($"Failed to load stored file by id '{id}' due to data mapping error.", ex);
            }
            catch (SqliteException ex)
            {
                throw new RepositoryException($"Failed to load stored file by id '{id}' due to database error.", ex);
            }
        }

        public async Task<StoredFile?> GetBySha256Async(string sha256, CancellationToken cancellationToken = default)
        {
            const string sql = """
                SELECT
                    id AS Id,
                    sha256 AS Sha256,
                    crc32 AS Crc32,
                    file_size AS FileSize,
                    physical_path AS PhysicalPath,
                    original_file_name AS OriginalFileName,
                    content_type AS ContentType,
                    filestore_state_compress AS FilestoreStateCompress,
                    reference_count AS ReferenceCount,
                    created_utc AS CreatedUtc,
                    updated_utc AS UpdatedUtc,
                    row_version AS RowVersion,
                    deleted_utc AS DeletedUtc,
                    is_deleted AS IsDeleted
                FROM stored_files
                WHERE sha256 = @Sha256
                  AND is_deleted = 0
                LIMIT 1;
                """;

            try
            {
                using var connection = _connectionFactory.CreateConnection();
                return await connection.QuerySingleOrDefaultAsync<StoredFile>(new CommandDefinition(
                    sql,
                    new { Sha256 = sha256 },
                    cancellationToken: cancellationToken));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (DataException ex)
            {
                throw new RepositoryException($"Failed to load stored file by sha256 '{sha256}' due to data mapping error.", ex);
            }
            catch (SqliteException ex)
            {
                throw new RepositoryException($"Failed to load stored file by sha256 '{sha256}' due to database error.", ex);
            }
        }

        public async Task<StoredFile?> GetByHashesAsync(string sha256, string crc32, CancellationToken cancellationToken = default)
        {
            const string sql = """
                SELECT
                    id AS Id,
                    sha256 AS Sha256,
                    crc32 AS Crc32,
                    file_size AS FileSize,
                    physical_path AS PhysicalPath,
                    original_file_name AS OriginalFileName,
                    content_type AS ContentType,
                    filestore_state_compress AS FilestoreStateCompress,
                    reference_count AS ReferenceCount,
                    created_utc AS CreatedUtc,
                    updated_utc AS UpdatedUtc,
                    row_version AS RowVersion,
                    deleted_utc AS DeletedUtc,
                    is_deleted AS IsDeleted
                FROM stored_files
                WHERE sha256 = @Sha256
                  AND crc32 = @Crc32
                  AND is_deleted = 0
                LIMIT 1;
                """;

            try
            {
                using var connection = _connectionFactory.CreateConnection();
                return await connection.QuerySingleOrDefaultAsync<StoredFile>(new CommandDefinition(
                    sql,
                    new
                    {
                        Sha256 = sha256,
                        Crc32 = crc32
                    },
                    cancellationToken: cancellationToken));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (DataException ex)
            {
                throw new RepositoryException($"Failed to load stored file by hashes '{sha256}' and '{crc32}' due to data mapping error.", ex);
            }
            catch (SqliteException ex)
            {
                throw new RepositoryException($"Failed to load stored file by hashes '{sha256}' and '{crc32}' due to database error.", ex);
            }
        }

        public async Task<IReadOnlyList<StoredFile>> GetActiveAsync(CancellationToken cancellationToken = default)
        {
            const string sql = """
                SELECT
                    id AS Id,
                    sha256 AS Sha256,
                    crc32 AS Crc32,
                    file_size AS FileSize,
                    physical_path AS PhysicalPath,
                    original_file_name AS OriginalFileName,
                    content_type AS ContentType,
                    filestore_state_compress AS FilestoreStateCompress,
                    reference_count AS ReferenceCount,
                    created_utc AS CreatedUtc,
                    updated_utc AS UpdatedUtc,
                    row_version AS RowVersion,
                    deleted_utc AS DeletedUtc,
                    is_deleted AS IsDeleted
                FROM stored_files
                WHERE is_deleted = 0
                ORDER BY id;
                """;

            try
            {
                using var connection = _connectionFactory.CreateConnection();
                var rows = await connection.QueryAsync<StoredFile>(new CommandDefinition(
                    sql,
                    cancellationToken: cancellationToken));

                return rows.ToList();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (DataException ex)
            {
                throw new RepositoryException("Failed to load active stored files due to data mapping error.", ex);
            }
            catch (SqliteException ex)
            {
                throw new RepositoryException("Failed to load active stored files due to database error.", ex);
            }
        }

        public async Task<bool> IncrementReferenceCountAsync(Guid id, CancellationToken cancellationToken = default)
        {
            const string sql = """
                UPDATE stored_files
                SET reference_count = reference_count + 1
                WHERE id = @Id
                  AND is_deleted = 0;
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
                throw new RepositoryException($"Failed to increment reference count for stored file '{id}' due to data mapping error.", ex);
            }
            catch (SqliteException ex)
            {
                throw new RepositoryException($"Failed to increment reference count for stored file '{id}' due to database error.", ex);
            }
        }

        public async Task<bool> DecrementReferenceCountAsync(Guid id, CancellationToken cancellationToken = default)
        {
            const string sql = """
                UPDATE stored_files
                SET reference_count = CASE
                    WHEN reference_count > 0 THEN reference_count - 1
                    ELSE 0
                END
                WHERE id = @Id
                  AND is_deleted = 0;
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
                throw new RepositoryException($"Failed to decrement reference count for stored file '{id}' due to data mapping error.", ex);
            }
            catch (SqliteException ex)
            {
                throw new RepositoryException($"Failed to decrement reference count for stored file '{id}' due to database error.", ex);
            }
        }

        public async Task<IReadOnlyList<StoredFile>> GetOrphanFilesAsync(CancellationToken cancellationToken = default)
        {
            const string sql = """
                SELECT
                    id AS Id,
                    sha256 AS Sha256,
                    crc32 AS Crc32,
                    file_size AS FileSize,
                    physical_path AS PhysicalPath,
                    original_file_name AS OriginalFileName,
                    content_type AS ContentType,
                    filestore_state_compress AS FilestoreStateCompress,
                    reference_count AS ReferenceCount,
                    created_utc AS CreatedUtc,
                    updated_utc AS UpdatedUtc,
                    row_version AS RowVersion,
                    deleted_utc AS DeletedUtc,
                    is_deleted AS IsDeleted
                FROM stored_files
                WHERE reference_count <= 0
                  AND is_deleted = 0
                ORDER BY id;
                """;

            try
            {
                using var connection = _connectionFactory.CreateConnection();
                var rows = await connection.QueryAsync<StoredFile>(new CommandDefinition(
                    sql,
                    cancellationToken: cancellationToken));

                return rows.ToList();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (DataException ex)
            {
                throw new RepositoryException("Failed to load orphan stored files due to data mapping error.", ex);
            }
            catch (SqliteException ex)
            {
                throw new RepositoryException("Failed to load orphan stored files due to database error.", ex);
            }
        }

        public async Task<bool> MarkDeletedAsync(Guid id, DateTime deletedUtc, CancellationToken cancellationToken = default)
        {
            const string sql = """
                UPDATE stored_files
                SET
                    is_deleted = 1,
                    deleted_utc = @DeletedUtc
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
                        DeletedUtc = deletedUtc
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
                throw new RepositoryException($"Failed to mark stored file '{id}' as deleted due to data mapping error.", ex);
            }
            catch (SqliteException ex)
            {
                throw new RepositoryException($"Failed to mark stored file '{id}' as deleted due to database error.", ex);
            }
        }

        public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            const string sql = "DELETE FROM stored_files WHERE id = @Id;";

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
                throw new RepositoryException($"Failed to delete stored file '{id}' due to data mapping error.", ex);
            }
            catch (SqliteException ex)
            {
                throw new RepositoryException($"Failed to delete stored file '{id}' due to database error.", ex);
            }
        }
    }
}
