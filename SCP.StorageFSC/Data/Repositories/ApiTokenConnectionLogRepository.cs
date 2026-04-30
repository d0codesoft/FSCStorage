using Dapper;
using SCP.StorageFSC.Data.Models;

namespace SCP.StorageFSC.Data.Repositories
{
    public sealed class ApiTokenConnectionLogRepository : IApiTokenConnectionLogRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public ApiTokenConnectionLogRepository(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public async Task<Guid> InsertAsync(ApiTokenConnectionLog log, CancellationToken cancellationToken = default)
        {
            const string sql = """
                INSERT INTO api_token_connection_logs
                (
                    id,
                    api_token_id,
                    token_name,
                    tenant_id,
                    external_tenant_id,
                    tenant_name,
                    is_success,
                    error_message,
                    is_admin,
                    client_ip,
                    ip_source,
                    forwarded_for_raw,
                    real_ip_raw,
                    request_path,
                    user_agent,
                    created_utc,
                    updated_utc,
                    row_version
                )
                VALUES
                (
                    @Id,
                    @ApiTokenId,
                    @TokenName,
                    @TenantId,
                    @ExternalTenantId,
                    @TenantName,
                    @IsSuccess,
                    @ErrorMessage,
                    @IsAdmin,
                    @ClientIp,
                    @IpSource,
                    @ForwardedForRaw,
                    @RealIpRaw,
                    @RequestPath,
                    @UserAgent,
                    @CreatedUtc,
                    @UpdatedUtc,
                    @RowVersion
                );
                """;

            using var connection = _connectionFactory.CreateConnection();

            var result = await connection.ExecuteAsync(new CommandDefinition(
                sql,
                new
                {
                    log.Id,
                    log.ApiTokenId,
                    log.TokenName,
                    log.TenantId,
                    ExternalTenantId = log.ExternalTenantId,
                    log.TenantName,
                    IsSuccess = log.IsSuccess ? 1 : 0,
                    log.ErrorMessage,
                    IsAdmin = log.IsAdmin ? 1 : 0,
                    log.ClientIp,
                    log.IpSource,
                    log.ForwardedForRaw,
                    log.RealIpRaw,
                    log.RequestPath,
                    log.UserAgent,
                    log.CreatedUtc,
                    log.UpdatedUtc,
                    log.RowVersion
                },
                cancellationToken: cancellationToken));
            
            if (result == 0)
            {
                throw new Exception("Failed to insert API token connection log.");
            }


            return log.Id;
        }

        public async Task<ApiTokenConnectionLog?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            const string sql = """
                SELECT
                    id AS Id,
                    api_token_id AS ApiTokenId,
                    token_name AS TokenName,
                    tenant_id AS TenantId,
                    external_tenant_id AS ExternalTenantId,
                    tenant_name AS TenantName,
                    is_success AS IsSuccess,
                    error_message AS ErrorMessage,
                    is_admin AS IsAdmin,
                    client_ip AS ClientIp,
                    ip_source AS IpSource,
                    forwarded_for_raw AS ForwardedForRaw,
                    real_ip_raw AS RealIpRaw,
                    request_path AS RequestPath,
                    user_agent AS UserAgent,
                    created_utc AS CreatedUtc,
                    updated_utc AS UpdatedUtc,
                    row_version AS RowVersion
                FROM api_token_connection_logs
                WHERE id = @Id
                LIMIT 1;
                """;

            using var connection = _connectionFactory.CreateConnection();
            return await connection.QuerySingleOrDefaultAsync<ApiTokenConnectionLog>(new CommandDefinition(
                sql,
                new { Id = id },
                cancellationToken: cancellationToken));
        }

        public async Task<IReadOnlyList<ApiTokenConnectionLog>> GetByApiTokenIdAsync(Guid apiTokenId, int take = 100, CancellationToken cancellationToken = default)
        {
            const string sql = """
                SELECT
                    id AS Id,
                    api_token_id AS ApiTokenId,
                    token_name AS TokenName,
                    tenant_id AS TenantId,
                    external_tenant_id AS ExternalTenantId,
                    tenant_name AS TenantName,
                    is_success AS IsSuccess,
                    error_message AS ErrorMessage,
                    is_admin AS IsAdmin,
                    client_ip AS ClientIp,
                    ip_source AS IpSource,
                    forwarded_for_raw AS ForwardedForRaw,
                    real_ip_raw AS RealIpRaw,
                    request_path AS RequestPath,
                    user_agent AS UserAgent,
                    created_utc AS CreatedUtc,
                    updated_utc AS UpdatedUtc,
                    row_version AS RowVersion
                FROM api_token_connection_logs
                WHERE api_token_id = @ApiTokenId
                ORDER BY created_utc DESC
                LIMIT @Take;
                """;

            using var connection = _connectionFactory.CreateConnection();
            var rows = await connection.QueryAsync<ApiTokenConnectionLog>(new CommandDefinition(
                sql,
                new
                {
                    ApiTokenId = apiTokenId,
                    Take = take
                },
                cancellationToken: cancellationToken));

            return rows.ToList();
        }

        public async Task<IReadOnlyList<ApiTokenConnectionLog>> GetByTenantIdAsync(Guid tenantId, int take = 100, CancellationToken cancellationToken = default)
        {
            const string sql = """
                SELECT
                    id AS Id,
                    api_token_id AS ApiTokenId,
                    token_name AS TokenName,
                    tenant_id AS TenantId,
                    external_tenant_id AS ExternalTenantId,
                    tenant_name AS TenantName,
                    is_success AS IsSuccess,
                    error_message AS ErrorMessage,
                    is_admin AS IsAdmin,
                    client_ip AS ClientIp,
                    ip_source AS IpSource,
                    forwarded_for_raw AS ForwardedForRaw,
                    real_ip_raw AS RealIpRaw,
                    request_path AS RequestPath,
                    user_agent AS UserAgent,
                    created_utc AS CreatedUtc,
                    updated_utc AS UpdatedUtc,
                    row_version AS RowVersion
                FROM api_token_connection_logs
                WHERE tenant_id = @TenantId
                ORDER BY created_utc DESC
                LIMIT @Take;
                """;

            using var connection = _connectionFactory.CreateConnection();
            var rows = await connection.QueryAsync<ApiTokenConnectionLog>(new CommandDefinition(
                sql,
                new
                {
                    TenantId = tenantId,
                    Take = take
                },
                cancellationToken: cancellationToken));

            return rows.ToList();
        }

        public async Task<IReadOnlyList<ApiTokenConnectionLog>> GetRecentAsync(int take = 100, CancellationToken cancellationToken = default)
        {
            const string sql = """
                SELECT
                    id AS Id,
                    public_id AS PublicId,
                    created_utc AS CreatedUtc,
                    updated_utc AS UpdatedUtc,
                    row_version AS RowVersion,
                    api_token_id AS ApiTokenId,
                    token_name AS TokenName,
                    tenant_id AS TenantId,
                    external_tenant_id AS ExternalTenantId,
                    tenant_name AS TenantName,
                    is_success AS IsSuccess,
                    error_message AS ErrorMessage,
                    is_admin AS IsAdmin,
                    client_ip AS ClientIp,
                    ip_source AS IpSource,
                    forwarded_for_raw AS ForwardedForRaw,
                    real_ip_raw AS RealIpRaw,
                    request_path AS RequestPath,
                    user_agent AS UserAgent
                FROM api_token_connection_logs
                ORDER BY created_utc DESC
                LIMIT @Take;
                """;

            using var connection = _connectionFactory.CreateConnection();
            var rows = await connection.QueryAsync<ApiTokenConnectionLog>(new CommandDefinition(
                sql,
                new { Take = take },
                cancellationToken: cancellationToken));

            return rows.ToList();
        }

        public async Task<IReadOnlyList<ApiTokenConnectionLog>> GetFailedAsync(int take = 100, CancellationToken cancellationToken = default)
        {
            const string sql = """
                SELECT
                    id AS Id,
                    api_token_id AS ApiTokenId,
                    token_name AS TokenName,
                    tenant_id AS TenantId,
                    external_tenant_id AS ExternalTenantId,
                    tenant_name AS TenantName,
                    is_success AS IsSuccess,
                    error_message AS ErrorMessage,
                    is_admin AS IsAdmin,
                    client_ip AS ClientIp,
                    ip_source AS IpSource,
                    forwarded_for_raw AS ForwardedForRaw,
                    real_ip_raw AS RealIpRaw,
                    request_path AS RequestPath,
                    user_agent AS UserAgent,
                    created_utc AS CreatedUtc,
                    updated_utc AS UpdatedUtc,
                    row_version AS RowVersion
                FROM api_token_connection_logs
                WHERE is_success = 0
                ORDER BY created_utc DESC
                LIMIT @Take;
                """;

            using var connection = _connectionFactory.CreateConnection();
            var rows = await connection.QueryAsync<ApiTokenConnectionLog>(new CommandDefinition(
                sql,
                new { Take = take },
                cancellationToken: cancellationToken));

            return rows.ToList();
        }

        public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            const string sql = "DELETE FROM api_token_connection_logs WHERE id = @Id;";

            using var connection = _connectionFactory.CreateConnection();

            var affected = await connection.ExecuteAsync(new CommandDefinition(
                sql,
                new { Id = id },
                cancellationToken: cancellationToken));

            return affected > 0;
        }
    }
}
