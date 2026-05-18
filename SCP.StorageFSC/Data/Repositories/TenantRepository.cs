using Dapper;
using Microsoft.Data.Sqlite;
using scp.filestorage.Data.Handlers;
using SCP.StorageFSC.Data.Models;
using System.Data;

namespace SCP.StorageFSC.Data.Repositories
{
    public sealed class TenantRepository : ITenantRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public TenantRepository(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public async Task<bool> InsertAsync(Tenant tenant, CancellationToken cancellationToken = default)
        {
            const string sql = """
                INSERT INTO tenants
                (
                    id,
                    user_id,
                    external_tenant_id,
                    name,
                    is_active,
                    total_size_bytes,
                    created_utc,
                    updated_utc,
                    row_version
                )
                VALUES
                (
                    @Id,
                    @UserId,
                    @ExternalTenantId,
                    @Name,
                    @IsActive,
                    @TotalSizeBytes,
                    @CreatedUtc,
                    @UpdatedUtc,
                    @RowVersion
                );
                """;

            try
            {
                using var connection = _connectionFactory.CreateConnection();

                var result = await connection.ExecuteAsync(new CommandDefinition(
                    sql,
                    new
                    {
                        Id = tenant.Id,
                        UserId = tenant.UserId,
                        ExternalTenantId = tenant.ExternalTenantId,
                        tenant.Name,
                        IsActive = tenant.IsActive ? 1 : 0,
                        tenant.TotalSizeBytes,
                        tenant.CreatedUtc,
                        tenant.UpdatedUtc,
                        RowVersion = tenant.RowVersion
                    },
                    cancellationToken: cancellationToken));

                return result > 0;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (DataException ex)
            {
                throw new RepositoryException($"Failed to insert tenant '{tenant.Id}' due to data mapping error.", ex);
            }
            catch (SqliteException ex)
            {
                throw new RepositoryException($"Failed to insert tenant '{tenant.Id}' due to database error.", ex);
            }
        }

        public async Task<Tenant?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            const string sql = """
                SELECT
                    id AS Id,
                    user_id AS UserId,
                    external_tenant_id AS ExternalTenantId,
                    name AS Name,
                    is_active AS IsActive,
                    total_size_bytes AS TotalSizeBytes,
                    created_utc AS CreatedUtc,
                    updated_utc AS UpdatedUtc,
                    row_version AS RowVersion
                FROM tenants
                WHERE id = @Id
                LIMIT 1;
                """;

            try
            {
                using var connection = _connectionFactory.CreateConnection();
                return await connection.QuerySingleOrDefaultAsync<Tenant>(new CommandDefinition(
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
                throw new RepositoryException($"Failed to load tenant by id '{id}' due to data mapping error.", ex);
            }
            catch (SqliteException ex)
            {
                throw new RepositoryException($"Failed to load tenant by id '{id}' due to database error.", ex);
            }
        }

        public async Task<Tenant?> GetByGuidAsync(Guid tenantGuid, CancellationToken cancellationToken = default)
        {
            const string sql = """
                SELECT
                    id AS Id,
                    user_id AS UserId,
                    external_tenant_id AS ExternalTenantId,
                    name AS Name,
                    is_active AS IsActive,
                    total_size_bytes AS TotalSizeBytes,
                    created_utc AS CreatedUtc,
                    updated_utc AS UpdatedUtc,
                    row_version AS RowVersion
                FROM tenants
                WHERE external_tenant_id = @TenantGuid
                LIMIT 1;
                """;

            try
            {
                using var connection = _connectionFactory.CreateConnection();
                return await connection.QuerySingleOrDefaultAsync<Tenant>(new CommandDefinition(
                    sql,
                    new { TenantGuid = tenantGuid },
                    cancellationToken: cancellationToken));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (DataException ex)
            {
                throw new RepositoryException($"Failed to load tenant by guid '{tenantGuid}' due to data mapping error.", ex);
            }
            catch (SqliteException ex)
            {
                throw new RepositoryException($"Failed to load tenant by guid '{tenantGuid}' due to database error.", ex);
            }
        }

        public async Task<Tenant?> GetFirstByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            const string sql = """
                SELECT
                    id AS Id,
                    user_id AS UserId,
                    external_tenant_id AS ExternalTenantId,
                    name AS Name,
                    is_active AS IsActive,
                    total_size_bytes AS TotalSizeBytes,
                    created_utc AS CreatedUtc,
                    updated_utc AS UpdatedUtc,
                    row_version AS RowVersion
                FROM tenants
                WHERE user_id = @UserId
                ORDER BY created_utc, id
                LIMIT 1;
                """;

            try
            {
                using var connection = _connectionFactory.CreateConnection();
                return await connection.QuerySingleOrDefaultAsync<Tenant>(new CommandDefinition(
                    sql,
                    new { UserId = userId },
                    cancellationToken: cancellationToken));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (DataException ex)
            {
                throw new RepositoryException($"Failed to load first tenant for user '{userId}' due to data mapping error.", ex);
            }
            catch (SqliteException ex)
            {
                throw new RepositoryException($"Failed to load first tenant for user '{userId}' due to database error.", ex);
            }
        }

        public async Task<IReadOnlyList<Tenant>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            const string sql = """
                SELECT
                    id AS Id,
                    user_id AS UserId,
                    external_tenant_id AS ExternalTenantId,
                    name AS Name,
                    is_active AS IsActive,
                    total_size_bytes AS TotalSizeBytes,
                    created_utc AS CreatedUtc,
                    updated_utc AS UpdatedUtc,
                    row_version AS RowVersion
                FROM tenants
                WHERE user_id = @UserId
                ORDER BY created_utc, id;
                """;

            try
            {
                using var connection = _connectionFactory.CreateConnection();
                var rows = await connection.QueryAsync<Tenant>(new CommandDefinition(
                    sql,
                    new { UserId = userId },
                    cancellationToken: cancellationToken));

                return rows.ToList();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (DataException ex)
            {
                throw new RepositoryException($"Failed to load tenants for user '{userId}' due to data mapping error.", ex);
            }
            catch (SqliteException ex)
            {
                throw new RepositoryException($"Failed to load tenants for user '{userId}' due to database error.", ex);
            }
        }

        public async Task<Tenant?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
        {
            const string sql = """
                SELECT
                    id AS Id,
                    user_id AS UserId,
                    external_tenant_id AS ExternalTenantId,
                    name AS Name,
                    is_active AS IsActive,
                    total_size_bytes AS TotalSizeBytes,
                    created_utc AS CreatedUtc,
                    updated_utc AS UpdatedUtc,
                    row_version AS RowVersion
                FROM tenants
                WHERE name = @Name
                LIMIT 1;
                """;

            try
            {
                using var connection = _connectionFactory.CreateConnection();
                return await connection.QuerySingleOrDefaultAsync<Tenant>(new CommandDefinition(
                    sql,
                    new { Name = name },
                    cancellationToken: cancellationToken));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (DataException ex)
            {
                throw new RepositoryException($"Failed to load tenant by name '{name}' due to data mapping error.", ex);
            }
            catch (SqliteException ex)
            {
                throw new RepositoryException($"Failed to load tenant by name '{name}' due to database error.", ex);
            }
        }

        public async Task<IReadOnlyList<Tenant>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            const string sql = """
                SELECT
                    id AS Id,
                    user_id AS UserId,
                    external_tenant_id AS ExternalTenantId,
                    name AS Name,
                    is_active AS IsActive,
                    created_utc AS CreatedUtc,
                    updated_utc AS UpdatedUtc,
                    row_version AS RowVersion
                FROM tenants
                ORDER BY id;
                """;

            try
            {
                using var connection = _connectionFactory.CreateConnection();
                var rows = await connection.QueryAsync<Tenant>(new CommandDefinition(
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
                throw new RepositoryException("Failed to load tenants due to data mapping error.", ex);
            }
            catch (SqliteException ex)
            {
                throw new RepositoryException("Failed to load tenants due to database error.", ex);
            }
        }

        public async Task<bool> UpdateAsync(Tenant tenant, CancellationToken cancellationToken = default)
        {
            const string sql = """
                UPDATE tenants
                SET
                    user_id = @UserId,
                    external_tenant_id = @ExternalTenantId,
                    name = @Name,
                    is_active = @IsActive,
                    total_size_bytes = @TotalSizeBytes,
                    updated_utc = @UpdatedUtc,
                    row_version = @RowVersion
                WHERE id = @Id;
                """;

            try
            {
                using var connection = _connectionFactory.CreateConnection();

                var affected = await connection.ExecuteAsync(new CommandDefinition(
                    sql,
                    new
                    {
                        Id = tenant.Id,
                        UserId = tenant.UserId,
                        ExternalTenantId = tenant.ExternalTenantId,
                        tenant.Name,
                        IsActive = tenant.IsActive ? 1 : 0,
                        tenant.TotalSizeBytes,
                        tenant.UpdatedUtc,
                        RowVersion = tenant.RowVersion
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
                throw new RepositoryException($"Failed to update tenant '{tenant.Id}' due to data mapping error.", ex);
            }
            catch (SqliteException ex)
            {
                throw new RepositoryException($"Failed to update tenant '{tenant.Id}' due to database error.", ex);
            }
        }

        public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            const string sql = "DELETE FROM tenants WHERE id = @Id;";

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
                throw new RepositoryException($"Failed to delete tenant '{id}' due to data mapping error.", ex);
            }
            catch (SqliteException ex)
            {
                throw new RepositoryException($"Failed to delete tenant '{id}' due to database error.", ex);
            }
        }

        public async Task<bool> RecalculateTotalSizeBytesAsync(Guid tenantId, CancellationToken cancellationToken = default)
        {
            const string sql = """
                UPDATE tenants
                SET
                    total_size_bytes = COALESCE(
                        (
                            SELECT SUM(sf.file_size)
                            FROM tenant_files tf
                            INNER JOIN stored_files sf ON sf.id = tf.stored_file_id
                            WHERE tf.tenant_id = tenants.id
                              AND tf.is_active = 1
                              AND sf.is_deleted = 0
                        ),
                        0
                    ),
                    updated_utc = @UpdatedUtc,
                    row_version = @RowVersion
                WHERE id = @TenantId;
                """;

            try
            {
                using var connection = _connectionFactory.CreateConnection();

                var affected = await connection.ExecuteAsync(new CommandDefinition(
                    sql,
                    new
                    {
                        TenantId = tenantId,
                        UpdatedUtc = DateTime.UtcNow,
                        RowVersion = Guid.CreateVersion7()
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
                throw new RepositoryException($"Failed to recalculate tenant storage usage for tenant '{tenantId}' due to data mapping error.", ex);
            }
            catch (SqliteException ex)
            {
                throw new RepositoryException($"Failed to recalculate tenant storage usage for tenant '{tenantId}' due to database error.", ex);
            }
        }

        public async Task<int> RecalculateAllTotalSizeBytesAsync(CancellationToken cancellationToken = default)
        {
            const string sql = """
                UPDATE tenants
                SET
                    total_size_bytes = COALESCE(
                        (
                            SELECT SUM(sf.file_size)
                            FROM tenant_files tf
                            INNER JOIN stored_files sf ON sf.id = tf.stored_file_id
                            WHERE tf.tenant_id = tenants.id
                              AND tf.is_active = 1
                              AND sf.is_deleted = 0
                        ),
                        0
                    ),
                    updated_utc = @UpdatedUtc,
                    row_version = randomblob(16);
                """;

            try
            {
                using var connection = _connectionFactory.CreateConnection();

                return await connection.ExecuteAsync(new CommandDefinition(
                    sql,
                    new
                    {
                        UpdatedUtc = DateTime.UtcNow
                    },
                    cancellationToken: cancellationToken));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (DataException ex)
            {
                throw new RepositoryException("Failed to recalculate tenant storage usage for all tenants due to data mapping error.", ex);
            }
            catch (SqliteException ex)
            {
                throw new RepositoryException("Failed to recalculate tenant storage usage for all tenants due to database error.", ex);
            }
        }
    }
}
