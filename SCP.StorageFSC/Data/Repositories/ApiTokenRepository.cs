using Dapper;
using SCP.StorageFSC.Data.Models;

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
            const string sql = """
                INSERT INTO api_tokens
                (
                    id,
                    public_id,
                    tenant_id,
                    name,
                    token_hash,
                    token_prefix,
                    is_active,
                    is_admin,
                    can_read,
                    can_write,
                    can_delete,
                    created_utc,
                    updated_utc,
                    row_version,
                    last_used_utc,
                    expires_utc,
                    revoked_utc
                )
                VALUES
                (
                    @Id,
                    @PublicId,
                    @TenantId,
                    @Name,
                    @TokenHash,
                    @TokenPrefix,
                    @IsActive,
                    @IsAdmin,
                    @CanRead,
                    @CanWrite,
                    @CanDelete,
                    @CreatedUtc,
                    @UpdatedUtc,
                    @RowVersion,
                    @LastUsedUtc,
                    @ExpiresUtc,
                    @RevokedUtc
                );
                """;

            using var connection = _connectionFactory.CreateConnection();

            await connection.ExecuteAsync(new CommandDefinition(
                sql,
                new
                {
                    token.Id,
                    token.PublicId,
                    token.TenantId,
                    token.Name,
                    token.TokenHash,
                    token.TokenPrefix,
                    IsActive = token.IsActive ? 1 : 0,
                    IsAdmin = token.IsAdmin ? 1 : 0,
                    CanRead = token.CanRead ? 1 : 0,
                    CanWrite = token.CanWrite ? 1 : 0,
                    CanDelete = token.CanDelete ? 1 : 0,
                    token.CreatedUtc,
                    token.UpdatedUtc,
                    token.RowVersion,
                    token.LastUsedUtc,
                    token.ExpiresUtc,
                    token.RevokedUtc
                },
                cancellationToken: cancellationToken));

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

            using var connection = _connectionFactory.CreateConnection();
            return await connection.QuerySingleOrDefaultAsync<ApiToken>(new CommandDefinition(
                sql,
                new { Id = id },
                cancellationToken: cancellationToken));
        }

        public async Task<ApiToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default)
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
                WHERE token_hash = @TokenHash
                LIMIT 1;
                """;

            using var connection = _connectionFactory.CreateConnection();
            return await connection.QuerySingleOrDefaultAsync<ApiToken>(new CommandDefinition(
                sql,
                new { TokenHash = tokenHash },
                cancellationToken: cancellationToken));
        }

        public async Task<ApiToken?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
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
                WHERE name = @Name
                LIMIT 1;
                """;

            using var connection = _connectionFactory.CreateConnection();
            return await connection.QuerySingleOrDefaultAsync<ApiToken>(new CommandDefinition(
                sql,
                new { Name = name },
                cancellationToken: cancellationToken));
        }

        public async Task<IReadOnlyList<ApiToken>> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default)
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

            using var connection = _connectionFactory.CreateConnection();
            var rows = await connection.QueryAsync<ApiToken>(new CommandDefinition(
                sql,
                new { TenantId = tenantId },
                cancellationToken: cancellationToken));

            return rows.ToList();
        }

        public async Task<bool> UpdateLastUsedAsync(Guid id, DateTime lastUsedUtc, CancellationToken cancellationToken = default)
        {
            const string sql = """
                UPDATE api_tokens
                SET last_used_utc = @LastUsedUtc
                WHERE id = @Id;
                """;

            using var connection = _connectionFactory.CreateConnection();
            var affected = await connection.ExecuteAsync(new CommandDefinition(
                sql,
                new
                {
                    Id = id,
                    LastUsedUtc = lastUsedUtc
                },
                cancellationToken: cancellationToken));

            return affected > 0;
        }

        public async Task<bool> RevokeAsync(Guid id, DateTime revokedUtc, CancellationToken cancellationToken = default)
        {
            const string sql = """
                UPDATE api_tokens
                SET
                    is_active = 0,
                    revoked_utc = @RevokedUtc
                WHERE id = @Id;
                """;

            using var connection = _connectionFactory.CreateConnection();
            var affected = await connection.ExecuteAsync(new CommandDefinition(
                sql,
                new
                {
                    Id = id,
                    RevokedUtc = revokedUtc
                },
                cancellationToken: cancellationToken));

            return affected > 0;
        }

        public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            const string sql = "DELETE FROM api_tokens WHERE id = @Id;";

            using var connection = _connectionFactory.CreateConnection();
            var affected = await connection.ExecuteAsync(new CommandDefinition(
                sql,
                new { Id = id },
                cancellationToken: cancellationToken));

            return affected > 0;
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

            using var connection = _connectionFactory.CreateConnection();

            var count = await connection.ExecuteScalarAsync<long>(new CommandDefinition(
                sql,
                cancellationToken: cancellationToken));

            return count > 0;
        }


    }
}
