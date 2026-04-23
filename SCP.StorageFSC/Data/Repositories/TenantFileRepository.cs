using Dapper;
using SCP.StorageFSC.Data.Models;

namespace SCP.StorageFSC.Data.Repositories
{
    public sealed class TenantFileRepository : ITenantFileRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public TenantFileRepository(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public async Task<Guid> InsertAsync(TenantFile tenantFile, CancellationToken cancellationToken = default)
        {
            const string sql = """
                INSERT INTO tenant_files
                (
                    id,
                    public_id,
                    tenant_id,
                    stored_file_id,
                    file_guid,
                    file_name,
                    category,
                    external_key,
                    is_active,
                    created_utc,
                    updated_utc,
                    row_version,
                    deleted_utc
                )
                VALUES
                (
                    @Id,
                    @PublicId,
                    @TenantId,
                    @StoredFileId,
                    @FileGuid,
                    @FileName,
                    @Category,
                    @ExternalKey,
                    @IsActive,
                    @CreatedUtc,
                    @UpdatedUtc,
                    @RowVersion,
                    @DeletedUtc
                );
                """;

            using var connection = _connectionFactory.CreateConnection();

            await connection.ExecuteAsync(new CommandDefinition(
                sql,
                new
                {
                    tenantFile.Id,
                    tenantFile.PublicId,
                    tenantFile.TenantId,
                    tenantFile.StoredFileId,
                    FileGuid = tenantFile.FileGuid.ToString(),
                    tenantFile.FileName,
                    tenantFile.Category,
                    tenantFile.ExternalKey,
                    IsActive = tenantFile.IsActive ? 1 : 0,
                    tenantFile.CreatedUtc,
                    tenantFile.UpdatedUtc,
                    tenantFile.RowVersion,
                    tenantFile.DeletedUtc
                },
                cancellationToken: cancellationToken));

            return tenantFile.Id;
        }

        public async Task<TenantFile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            const string sql = """
                SELECT
                    id AS Id,
                    public_id AS PublicId,
                    tenant_id AS TenantId,
                    stored_file_id AS StoredFileId,
                    file_guid AS FileGuid,
                    file_name AS FileName,
                    category AS Category,
                    external_key AS ExternalKey,
                    is_active AS IsActive,
                    created_utc AS CreatedUtc,
                    updated_utc AS UpdatedUtc,
                    row_version AS RowVersion,
                    deleted_utc AS DeletedUtc
                FROM tenant_files
                WHERE id = @Id
                LIMIT 1;
                """;

            using var connection = _connectionFactory.CreateConnection();
            return await connection.QuerySingleOrDefaultAsync<TenantFile>(new CommandDefinition(
                sql,
                new { Id = id },
                cancellationToken: cancellationToken));
        }

        public async Task<TenantFile?> GetByFileGuidAsync(Guid fileGuid, CancellationToken cancellationToken = default)
        {
            const string sql = """
                SELECT
                    id AS Id,
                    public_id AS PublicId,
                    tenant_id AS TenantId,
                    stored_file_id AS StoredFileId,
                    file_guid AS FileGuid,
                    file_name AS FileName,
                    category AS Category,
                    external_key AS ExternalKey,
                    is_active AS IsActive,
                    created_utc AS CreatedUtc,
                    updated_utc AS UpdatedUtc,
                    row_version AS RowVersion,
                    deleted_utc AS DeletedUtc
                FROM tenant_files
                WHERE file_guid = @FileGuid
                  AND is_active = 1
                LIMIT 1;
                """;

            using var connection = _connectionFactory.CreateConnection();
            return await connection.QuerySingleOrDefaultAsync<TenantFile>(new CommandDefinition(
                sql,
                new { FileGuid = fileGuid.ToString() },
                cancellationToken: cancellationToken));
        }

        public async Task<TenantFile?> GetByTenantAndFileGuidAsync(Guid tenantId, Guid fileGuid, CancellationToken cancellationToken = default)
        {
            const string sql = """
                SELECT
                    id AS Id,
                    public_id AS PublicId,
                    tenant_id AS TenantId,
                    stored_file_id AS StoredFileId,
                    file_guid AS FileGuid,
                    file_name AS FileName,
                    category AS Category,
                    external_key AS ExternalKey,
                    is_active AS IsActive,
                    created_utc AS CreatedUtc,
                    updated_utc AS UpdatedUtc,
                    row_version AS RowVersion,
                    deleted_utc AS DeletedUtc
                FROM tenant_files
                WHERE tenant_id = @TenantId
                  AND file_guid = @FileGuid
                  AND is_active = 1
                LIMIT 1;
                """;

            using var connection = _connectionFactory.CreateConnection();
            return await connection.QuerySingleOrDefaultAsync<TenantFile>(new CommandDefinition(
                sql,
                new
                {
                    TenantId = tenantId,
                    FileGuid = fileGuid.ToString()
                },
                cancellationToken: cancellationToken));
        }

        public async Task<TenantFile?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
        {
            const string sql = """
                SELECT
                    id AS Id,
                    public_id AS PublicId,
                    tenant_id AS TenantId,
                    stored_file_id AS StoredFileId,
                    file_guid AS FileGuid,
                    file_name AS FileName,
                    category AS Category,
                    external_key AS ExternalKey,
                    is_active AS IsActive,
                    created_utc AS CreatedUtc,
                    updated_utc AS UpdatedUtc,
                    row_version AS RowVersion,
                    deleted_utc AS DeletedUtc
                FROM tenant_files
                WHERE file_name = @Name
                  AND is_active = 1
                LIMIT 1;
                """;

            using var connection = _connectionFactory.CreateConnection();
            return await connection.QuerySingleOrDefaultAsync<TenantFile>(new CommandDefinition(
                sql,
                new { Name = name },
                cancellationToken: cancellationToken));
        }

        public async Task<IReadOnlyList<TenantFile>> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default)
        {
            const string sql = """
                SELECT
                    id AS Id,
                    public_id AS PublicId,
                    tenant_id AS TenantId,
                    stored_file_id AS StoredFileId,
                    file_guid AS FileGuid,
                    file_name AS FileName,
                    category AS Category,
                    external_key AS ExternalKey,
                    is_active AS IsActive,
                    created_utc AS CreatedUtc,
                    updated_utc AS UpdatedUtc,
                    row_version AS RowVersion,
                    deleted_utc AS DeletedUtc
                FROM tenant_files
                WHERE tenant_id = @TenantId
                  AND is_active = 1
                ORDER BY id;
                """;

            using var connection = _connectionFactory.CreateConnection();
            var rows = await connection.QueryAsync<TenantFile>(new CommandDefinition(
                sql,
                new { TenantId = tenantId },
                cancellationToken: cancellationToken));

            return rows.ToList();
        }

        public async Task<IReadOnlyList<TenantFile>> GetByStoredFileIdAsync(Guid storedFileId, CancellationToken cancellationToken = default)
        {
            const string sql = """
                SELECT
                    id AS Id,
                    public_id AS PublicId,
                    tenant_id AS TenantId,
                    stored_file_id AS StoredFileId,
                    file_guid AS FileGuid,
                    file_name AS FileName,
                    category AS Category,
                    external_key AS ExternalKey,
                    is_active AS IsActive,
                    created_utc AS CreatedUtc,
                    updated_utc AS UpdatedUtc,
                    row_version AS RowVersion,
                    deleted_utc AS DeletedUtc
                FROM tenant_files
                WHERE stored_file_id = @StoredFileId
                  AND is_active = 1
                ORDER BY id;
                """;

            using var connection = _connectionFactory.CreateConnection();
            var rows = await connection.QueryAsync<TenantFile>(new CommandDefinition(
                sql,
                new { StoredFileId = storedFileId },
                cancellationToken: cancellationToken));

            return rows.ToList();
        }

        public async Task<TenantFile?> GetByTenantAndExternalKeyAsync(Guid tenantId, string externalKey, CancellationToken cancellationToken = default)
        {
            const string sql = """
                SELECT
                    id AS Id,
                    public_id AS PublicId,
                    tenant_id AS TenantId,
                    stored_file_id AS StoredFileId,
                    file_guid AS FileGuid,
                    file_name AS FileName,
                    category AS Category,
                    external_key AS ExternalKey,
                    is_active AS IsActive,
                    created_utc AS CreatedUtc,
                    updated_utc AS UpdatedUtc,
                    row_version AS RowVersion,
                    deleted_utc AS DeletedUtc
                FROM tenant_files
                WHERE tenant_id = @TenantId
                  AND external_key = @ExternalKey
                  AND is_active = 1
                LIMIT 1;
                """;

            using var connection = _connectionFactory.CreateConnection();
            return await connection.QuerySingleOrDefaultAsync<TenantFile>(new CommandDefinition(
                sql,
                new
                {
                    TenantId = tenantId,
                    ExternalKey = externalKey
                },
                cancellationToken: cancellationToken));
        }

        public async Task<bool> SoftDeleteAsync(Guid id, DateTime deletedUtc, CancellationToken cancellationToken = default)
        {
            const string sql = """
                UPDATE tenant_files
                SET
                    is_active = 0,
                    deleted_utc = @DeletedUtc
                WHERE id = @Id
                  AND is_active = 1;
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
            const string sql = "DELETE FROM tenant_files WHERE id = @Id;";

            using var connection = _connectionFactory.CreateConnection();
            var affected = await connection.ExecuteAsync(new CommandDefinition(
                sql,
                new { Id = id },
                cancellationToken: cancellationToken));

            return affected > 0;
        }
    }
}
