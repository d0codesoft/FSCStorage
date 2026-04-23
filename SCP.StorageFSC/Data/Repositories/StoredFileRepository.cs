using Dapper;
using SCP.StorageFSC.Data.Models;

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
                    public_id,
                    sha256,
                    crc32,
                    file_size,
                    physical_path,
                    original_file_name,
                    content_type,
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
                    @PublicId,
                    @Sha256,
                    @Crc32,
                    @FileSize,
                    @PhysicalPath,
                    @OriginalFileName,
                    @ContentType,
                    @ReferenceCount,
                    @CreatedUtc,
                    @UpdatedUtc,
                    @RowVersion,
                    @DeletedUtc,
                    @IsDeleted
                );
                """;

            using var connection = _connectionFactory.CreateConnection();

            await connection.ExecuteAsync(new CommandDefinition(
                sql,
                new
                {
                    file.Id,
                    file.PublicId,
                    file.Sha256,
                    file.Crc32,
                    file.FileSize,
                    file.PhysicalPath,
                    file.OriginalFileName,
                    file.ContentType,
                    file.ReferenceCount,
                    file.CreatedUtc,
                    file.UpdatedUtc,
                    file.RowVersion,
                    file.DeletedUtc,
                    IsDeleted = file.IsDeleted ? 1 : 0
                },
                cancellationToken: cancellationToken));

            return file.Id;
        }

        public async Task<StoredFile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            const string sql = """
                SELECT
                    id AS Id,
                    public_id AS PublicId,
                    sha256 AS Sha256,
                    crc32 AS Crc32,
                    file_size AS FileSize,
                    physical_path AS PhysicalPath,
                    original_file_name AS OriginalFileName,
                    content_type AS ContentType,
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

            using var connection = _connectionFactory.CreateConnection();
            return await connection.QuerySingleOrDefaultAsync<StoredFile>(new CommandDefinition(
                sql,
                new { Id = id },
                cancellationToken: cancellationToken));
        }

        public async Task<StoredFile?> GetBySha256Async(string sha256, CancellationToken cancellationToken = default)
        {
            const string sql = """
                SELECT
                    id AS Id,
                    public_id AS PublicId,
                    sha256 AS Sha256,
                    crc32 AS Crc32,
                    file_size AS FileSize,
                    physical_path AS PhysicalPath,
                    original_file_name AS OriginalFileName,
                    content_type AS ContentType,
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

            using var connection = _connectionFactory.CreateConnection();
            return await connection.QuerySingleOrDefaultAsync<StoredFile>(new CommandDefinition(
                sql,
                new { Sha256 = sha256 },
                cancellationToken: cancellationToken));
        }

        public async Task<StoredFile?> GetByHashesAsync(string sha256, string crc32, CancellationToken cancellationToken = default)
        {
            const string sql = """
                SELECT
                    id AS Id,
                    public_id AS PublicId,
                    sha256 AS Sha256,
                    crc32 AS Crc32,
                    file_size AS FileSize,
                    physical_path AS PhysicalPath,
                    original_file_name AS OriginalFileName,
                    content_type AS ContentType,
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

        public async Task<bool> IncrementReferenceCountAsync(Guid id, CancellationToken cancellationToken = default)
        {
            const string sql = """
                UPDATE stored_files
                SET reference_count = reference_count + 1
                WHERE id = @Id
                  AND is_deleted = 0;
                """;

            using var connection = _connectionFactory.CreateConnection();
            var affected = await connection.ExecuteAsync(new CommandDefinition(
                sql,
                new { Id = id },
                cancellationToken: cancellationToken));

            return affected > 0;
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

            using var connection = _connectionFactory.CreateConnection();
            var affected = await connection.ExecuteAsync(new CommandDefinition(
                sql,
                new { Id = id },
                cancellationToken: cancellationToken));

            return affected > 0;
        }

        public async Task<IReadOnlyList<StoredFile>> GetOrphanFilesAsync(CancellationToken cancellationToken = default)
        {
            const string sql = """
                SELECT
                    id AS Id,
                    public_id AS PublicId,
                    sha256 AS Sha256,
                    crc32 AS Crc32,
                    file_size AS FileSize,
                    physical_path AS PhysicalPath,
                    original_file_name AS OriginalFileName,
                    content_type AS ContentType,
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

            using var connection = _connectionFactory.CreateConnection();
            var rows = await connection.QueryAsync<StoredFile>(new CommandDefinition(
                sql,
                cancellationToken: cancellationToken));

            return rows.ToList();
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

        public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            const string sql = "DELETE FROM stored_files WHERE id = @Id;";

            using var connection = _connectionFactory.CreateConnection();
            var affected = await connection.ExecuteAsync(new CommandDefinition(
                sql,
                new { Id = id },
                cancellationToken: cancellationToken));

            return affected > 0;
        }
    }
}
