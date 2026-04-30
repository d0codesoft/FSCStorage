using Dapper;
using Microsoft.Data.Sqlite;
using scp.filestorage.Data.Handlers;
using SCP.StorageFSC.Data.Models;
using System.Data;

namespace SCP.StorageFSC.Data.Repositories
{
    public sealed class ApiTokenRepository : IApiTokenRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public ApiTokenRepository(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public async Task<Guid> InsertAsync(ApiToken token, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(token);

            const string sql = """
                INSERT INTO api_tokens
                (
                    id,
                    tenant_id,
                    name,
                    token_hash,
                    token_prefix,
                    is_active,
                    is_admin,
                    can_read,
                    can_write,
                    can_delete,
                    last_used_utc,
                    expires_utc,
                    revoked_utc,
                    created_utc,
                    updated_utc,
                    row_version
                )
                VALUES
                (
                    @Id,
                    @TenantId,
                    @Name,
                    @TokenHash,
                    @TokenPrefix,
                    @IsActive,
                    @IsAdmin,
                    @CanRead,
                    @CanWrite,
                    @CanDelete,
                    @LastUsedUtc,
                    @ExpiresUtc,
                    @RevokedUtc,
                    @CreatedUtc,
                    @UpdatedUtc,
                    @RowVersion
                );
                """;

            try
            {
                using var connection = _connectionFactory.CreateConnection();

                var affected = await connection.ExecuteAsync(new CommandDefinition(
                    sql,
                    new
                    {
                        token.Id,
                        token.TenantId,
                        token.Name,
                        token.TokenHash,
                        token.TokenPrefix,
                        IsActive = token.IsActive ? 1 : 0,
                        IsAdmin = token.IsAdmin ? 1 : 0,
                        CanRead = token.CanRead ? 1 : 0,
                        CanWrite = token.CanWrite ? 1 : 0,
                        CanDelete = token.CanDelete ? 1 : 0,
                        token.LastUsedUtc,
                        token.ExpiresUtc,
                        token.RevokedUtc,
                        token.CreatedUtc,
                        token.UpdatedUtc,
                        token.RowVersion
                    },
                    cancellationToken: cancellationToken));

                if (affected == 0)
                    throw new RepositoryException("Failed to insert API token.", new InvalidOperationException("Insert command affected 0 rows."));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (DataException ex)
            {
                throw new RepositoryException("Failed to insert API token due to data mapping error.", ex);
            }
            catch (SqliteException ex)
            {
                throw new RepositoryException("Failed to insert API token due to database error.", ex);
            }

            return token.Id;
        }

        public async Task<ApiToken?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            const string sql = """
                SELECT
                    id AS Id,
                    public_id AS PublicId,
                    tenant_id AS TenantId,
                    name AS Name,
                    token_hash AS TokenHash,
                    token_prefix AS TokenPrefix,
                    is_active AS IsActive,
                    is_admin AS IsAdmin,
                    can_read AS CanRead,
                    can_write AS CanWrite,
                    can_delete AS CanDelete,
                    created_utc AS CreatedUtc,
                    updated_utc AS UpdatedUtc,
                    row_version AS RowVersion,
                    last_used_utc AS LastUsedUtc,
                    expires_utc AS ExpiresUtc,
                    revoked_utc AS RevokedUtc
                FROM api_tokens
                WHERE id = @Id
                LIMIT 1;
                """;

            try
            {
                using var connection = _connectionFactory.CreateConnection();
                return await connection.QuerySingleOrDefaultAsync<ApiToken>(new CommandDefinition(
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
                throw new RepositoryException($"Failed to load API token by id '{id}' due to data mapping error.", ex);
            }
            catch (SqliteException ex)
            {
                throw new RepositoryException($"Failed to load API token by id '{id}' due to database error.", ex);
            }
        }

        public async Task<ApiToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default)
        {
            const string sql = """
                SELECT
                    id AS Id,
                    tenant_id AS TenantId,
                    name AS Name,
                    token_hash AS TokenHash,
                    token_prefix AS TokenPrefix,
                    is_active AS IsActive,
                    is_admin AS IsAdmin,
                    can_read AS CanRead,
                    can_write AS CanWrite,
                    can_delete AS CanDelete,
                    created_utc AS CreatedUtc,
                    updated_utc AS UpdatedUtc,
                    row_version AS RowVersion,
                    last_used_utc AS LastUsedUtc,
                    expires_utc AS ExpiresUtc,
                    revoked_utc AS RevokedUtc
                FROM api_tokens
                WHERE token_hash = @TokenHash
                LIMIT 1;
                """;

            try
            {
                using var connection = _connectionFactory.CreateConnection();
                return await connection.QuerySingleOrDefaultAsync<ApiToken>(new CommandDefinition(
                    sql,
                    new { TokenHash = tokenHash },
                    cancellationToken: cancellationToken));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (DataException ex)
            {
                throw new RepositoryException("Failed to load API token by hash due to data mapping error.", ex);
            }
            catch (SqliteException ex)
            {
                throw new RepositoryException("Failed to load API token by hash due to database error.", ex);
            }
        }

        public async Task<ApiToken?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
        {
            const string sql = """
                SELECT
                    id AS Id,
                    tenant_id AS TenantId,
                    name AS Name,
                    token_hash AS TokenHash,
                    token_prefix AS TokenPrefix,
                    is_active AS IsActive,
                    is_admin AS IsAdmin,
                    can_read AS CanRead,
                    can_write AS CanWrite,
                    can_delete AS CanDelete,
                    created_utc AS CreatedUtc,
                    updated_utc AS UpdatedUtc,
                    row_version AS RowVersion,
                    last_used_utc AS LastUsedUtc,
                    expires_utc AS ExpiresUtc,
                    revoked_utc AS RevokedUtc
                FROM api_tokens
                WHERE name = @Name
                LIMIT 1;
                """;

            try
            {
                using var connection = _connectionFactory.CreateConnection();
                return await connection.QuerySingleOrDefaultAsync<ApiToken>(new CommandDefinition(
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
                throw new RepositoryException($"Failed to load API token by name '{name}' due to data mapping error.", ex);
            }
            catch (SqliteException ex)
            {
                throw new RepositoryException($"Failed to load API token by name '{name}' due to database error.", ex);
            }
        }

        public async Task<IReadOnlyList<ApiToken>> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default)
        {
            const string sql = """
                SELECT
                    id AS Id,
                    tenant_id AS TenantId,
                    name AS Name,
                    token_hash AS TokenHash,
                    token_prefix AS TokenPrefix,
                    is_active AS IsActive,
                    is_admin AS IsAdmin,
                    can_read AS CanRead,
                    can_write AS CanWrite,
                    can_delete AS CanDelete,
                    created_utc AS CreatedUtc,
                    updated_utc AS UpdatedUtc,
                    row_version AS RowVersion,
                    last_used_utc AS LastUsedUtc,
                    expires_utc AS ExpiresUtc,
                    revoked_utc AS RevokedUtc
                FROM api_tokens
                WHERE tenant_id = @TenantId
                ORDER BY id;
                """;

            try
            {
                using var connection = _connectionFactory.CreateConnection();
                var rows = await connection.QueryAsync<ApiToken>(new CommandDefinition(
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
                throw new RepositoryException($"Failed to load API tokens for tenant '{tenantId}' due to data mapping error.", ex);
            }
            catch (SqliteException ex)
            {
                throw new RepositoryException($"Failed to load API tokens for tenant '{tenantId}' due to database error.", ex);
            }
        }

        public async Task<bool> UpdateLastUsedAsync(Guid id, DateTime lastUsedUtc, CancellationToken cancellationToken = default)
        {
            const string sql = """
                UPDATE api_tokens
                SET 
                    last_used_utc = @LastUsedUtc,
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
                        Id = id,
                        LastUsedUtc = lastUsedUtc,
                        UpdatedUtc = DateTime.UtcNow,
                        RowVersion = Guid.NewGuid()
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
                throw new RepositoryException($"Failed to update last-used timestamp for API token '{id}' due to data mapping error.", ex);
            }
            catch (SqliteException ex)
            {
                throw new RepositoryException($"Failed to update last-used timestamp for API token '{id}' due to database error.", ex);
            }
        }

        public async Task<bool> UpdateLastUsedAsync(ApiToken token, CancellationToken cancellationToken = default)
        {
            const string sql = """
                UPDATE api_tokens
                SET 
                    last_used_utc = @LastUsedUtc,
                    updated_utc = @UpdatedUtc,
                    row_version = @RowVersion
                WHERE id = @Id;
                """;

            token.MarkUpdated();

            try
            {
                using var connection = _connectionFactory.CreateConnection();

                var affected = await connection.ExecuteAsync(new CommandDefinition(
                    sql,
                    new
                    {
                        Id = token.Id,
                        LastUsedUtc = token.LastUsedUtc,
                        UpdatedUtc = token.UpdatedUtc,
                        RowVersion = token.RowVersion
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
                throw new RepositoryException($"Failed to update last-used timestamp for API token '{token.Id}' due to data mapping error.", ex);
            }
            catch (SqliteException ex)
            {
                throw new RepositoryException($"Failed to update last-used timestamp for API token '{token.Id}' due to database error.", ex);
            }
        }

        public async Task<bool> RevokeAsync(Guid id, DateTime revokedUtc, CancellationToken cancellationToken = default)
        {
            const string sql = """
                UPDATE api_tokens
                SET
                    is_active = 0,
                    revoked_utc = @RevokedUtc
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
                        Id = id,
                        RevokedUtc = revokedUtc,
                        UpdatedUtc = DateTime.UtcNow,
                        RowVersion = Guid.NewGuid()
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
                throw new RepositoryException($"Failed to revoke API token '{id}' due to data mapping error.", ex);
            }
            catch (SqliteException ex)
            {
                throw new RepositoryException($"Failed to revoke API token '{id}' due to database error.", ex);
            }
        }

        public async Task<bool> RevokeAsync(ApiToken token, CancellationToken cancellationToken = default)
        {
            const string sql = """
                UPDATE api_tokens
                SET
                    is_active = 0,
                    revoked_utc = @RevokedUtc
                    updated_utc = @UpdatedUtc,
                    row_version = @RowVersion
                WHERE id = @Id;
                """;

            token.MarkUpdated();

            try
            {
                using var connection = _connectionFactory.CreateConnection();

                var affected = await connection.ExecuteAsync(new CommandDefinition(
                    sql,
                    new
                    {
                        Id = token.Id,
                        RevokedUtc = token.RevokedUtc,
                        UpdatedUtc = token.UpdatedUtc,
                        RowVersion = token.RowVersion
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
                throw new RepositoryException($"Failed to revoke API token '{token.Id}' due to data mapping error.", ex);
            }
            catch (SqliteException ex)
            {
                throw new RepositoryException($"Failed to revoke API token '{token.Id}' due to database error.", ex);
            }
        }

        public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            const string sql = "DELETE FROM api_tokens WHERE id = @Id;";

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
                throw new RepositoryException($"Failed to delete API token '{id}' due to data mapping error.", ex);
            }
            catch (SqliteException ex)
            {
                throw new RepositoryException($"Failed to delete API token '{id}' due to database error.", ex);
            }
        }

        public async Task<bool> HasAnyAdminTokenAsync(CancellationToken cancellationToken = default)
        {
            const string sql = """
                SELECT COUNT(1)
                FROM api_tokens
                WHERE is_admin = 1
                  AND is_active = 1
                  AND revoked_utc IS NULL;
                """;

            try
            {
                using var connection = _connectionFactory.CreateConnection();

                var count = await connection.ExecuteScalarAsync<long>(new CommandDefinition(
                    sql,
                    cancellationToken: cancellationToken));

                return count > 0;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (DataException ex)
            {
                throw new RepositoryException("Failed to determine whether any admin API tokens exist due to data mapping error.", ex);
            }
            catch (SqliteException ex)
            {
                throw new RepositoryException("Failed to determine whether any admin API tokens exist due to database error.", ex);
            }
        }
    }
}
