using Dapper;
using SCP.StorageFSC.Data.Models;

namespace SCP.StorageFSC.Data.Repositories
{
    public sealed class TenantRepository : ITenantRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public TenantRepository(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public async Task<Guid> InsertAsync(Tenant tenant, CancellationToken cancellationToken = default)
        {
            const string sql = """
                INSERT INTO tenants
                (
                    id,
                    public_id,
                    tenant_guid,
                    name,
                    is_active,
                    created_utc,
                    updated_utc,
                    row_version
                )
                VALUES
                (
                    @Id,
                    @PublicId,
                    @TenantGuid,
                    @Name,
                    @IsActive,
                    @CreatedUtc,
                    @UpdatedUtc,
                    @RowVersion
                );
                """;

            using var connection = _connectionFactory.CreateConnection();

            await connection.ExecuteAsync(new CommandDefinition(
                sql,
                new
                {
                    tenant.Id,
                    tenant.PublicId,
                    TenantGuid = tenant.TenantGuid.ToString(),
                    tenant.Name,
                    IsActive = tenant.IsActive ? 1 : 0,
                    tenant.CreatedUtc,
                    tenant.UpdatedUtc,
                    tenant.RowVersion
                },
                cancellationToken: cancellationToken));

            return tenant.Id;
        }

        public async Task<Tenant?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            const string sql = """
                SELECT
                    id AS Id,
                    public_id AS PublicId,
                    tenant_guid AS TenantGuid,
                    name AS Name,
                    is_active AS IsActive,
                    created_utc AS CreatedUtc,
                    updated_utc AS UpdatedUtc,
                    row_version AS RowVersion
                FROM tenants
                WHERE id = @Id
                LIMIT 1;
                """;

            using var connection = _connectionFactory.CreateConnection();
            return await connection.QuerySingleOrDefaultAsync<Tenant>(new CommandDefinition(
                sql,
                new { Id = id },
                cancellationToken: cancellationToken));
        }

        public async Task<Tenant?> GetByGuidAsync(Guid tenantGuid, CancellationToken cancellationToken = default)
        {
            const string sql = """
                SELECT
                    id AS Id,
                    public_id AS PublicId,
                    tenant_guid AS TenantGuid,
                    name AS Name,
                    is_active AS IsActive,
                    created_utc AS CreatedUtc,
                    updated_utc AS UpdatedUtc,
                    row_version AS RowVersion
                FROM tenants
                WHERE tenant_guid = @TenantGuid
                LIMIT 1;
                """;

            using var connection = _connectionFactory.CreateConnection();
            return await connection.QuerySingleOrDefaultAsync<Tenant>(new CommandDefinition(
                sql,
                new { TenantGuid = tenantGuid.ToString() },
                cancellationToken: cancellationToken));
        }

        public async Task<Tenant?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
        {
            const string sql = """
                SELECT
                    id AS Id,
                    public_id AS PublicId,
                    tenant_guid AS TenantGuid,
                    name AS Name,
                    is_active AS IsActive,
                    created_utc AS CreatedUtc,
                    updated_utc AS UpdatedUtc,
                    row_version AS RowVersion
                FROM tenants
                WHERE name = @Name
                LIMIT 1;
                """;

            using var connection = _connectionFactory.CreateConnection();
            return await connection.QuerySingleOrDefaultAsync<Tenant>(new CommandDefinition(
                sql,
                new { Name = name },
                cancellationToken: cancellationToken));
        }

        public async Task<IReadOnlyList<Tenant>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            const string sql = """
                SELECT
                    id AS Id,
                    public_id AS PublicId,
                    tenant_guid AS TenantGuid,
                    name AS Name,
                    is_active AS IsActive,
                    created_utc AS CreatedUtc,
                    updated_utc AS UpdatedUtc,
                    row_version AS RowVersion
                FROM tenants
                ORDER BY id;
                """;

            using var connection = _connectionFactory.CreateConnection();
            var rows = await connection.QueryAsync<Tenant>(new CommandDefinition(
                sql,
                cancellationToken: cancellationToken));

            return rows.ToList();
        }

        public async Task<bool> UpdateAsync(Tenant tenant, CancellationToken cancellationToken = default)
        {
            const string sql = """
                UPDATE tenants
                SET
                    tenant_guid = @TenantGuid,
                    name = @Name,
                    is_active = @IsActive,
                    updated_utc = @UpdatedUtc,
                    row_version = @RowVersion
                WHERE id = @Id;
                """;

            using var connection = _connectionFactory.CreateConnection();

            var affected = await connection.ExecuteAsync(new CommandDefinition(
                sql,
                new
                {
                    tenant.Id,
                    TenantGuid = tenant.TenantGuid.ToString(),
                    tenant.Name,
                    IsActive = tenant.IsActive ? 1 : 0,
                    tenant.UpdatedUtc,
                    tenant.RowVersion
                },
                cancellationToken: cancellationToken));

            return affected > 0;
        }

        public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            const string sql = "DELETE FROM tenants WHERE id = @Id;";

            using var connection = _connectionFactory.CreateConnection();

            var affected = await connection.ExecuteAsync(new CommandDefinition(
                sql,
                new { Id = id },
                cancellationToken: cancellationToken));

            return affected > 0;
        }
    }
}
