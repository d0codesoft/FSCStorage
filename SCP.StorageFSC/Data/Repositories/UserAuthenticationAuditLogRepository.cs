using Dapper;
using SCP.StorageFSC.Data.Models;

namespace SCP.StorageFSC.Data.Repositories
{
    public sealed class UserAuthenticationAuditLogRepository : IUserAuthenticationAuditLogRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public UserAuthenticationAuditLogRepository(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public async Task<Guid> InsertAsync(UserAuthenticationAuditLog log, CancellationToken cancellationToken = default)
        {
            const string sql = """
                INSERT INTO user_authentication_audit_logs
                (
                    id,
                    user_id,
                    user_name,
                    login,
                    event_type,
                    status,
                    is_success,
                    failure_reason,
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
                    @UserId,
                    @UserName,
                    @Login,
                    @EventType,
                    @Status,
                    @IsSuccess,
                    @FailureReason,
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
                    log.UserId,
                    log.UserName,
                    log.Login,
                    log.EventType,
                    log.Status,
                    IsSuccess = log.IsSuccess ? 1 : 0,
                    log.FailureReason,
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
                throw new Exception("Failed to insert user authentication audit log.");
            }

            return log.Id;
        }

        public async Task<UserAuthenticationAuditLog?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            const string sql = $"""
                {SelectSql}
                WHERE id = @Id
                LIMIT 1;
                """;

            using var connection = _connectionFactory.CreateConnection();
            return await connection.QuerySingleOrDefaultAsync<UserAuthenticationAuditLog>(new CommandDefinition(
                sql,
                new { Id = id },
                cancellationToken: cancellationToken));
        }

        public async Task<int> CountAsync(Guid? userId = null, CancellationToken cancellationToken = default)
        {
            const string sql = """
                SELECT COUNT(1)
                FROM user_authentication_audit_logs
                WHERE @UserId IS NULL OR user_id = @UserId;
                """;

            using var connection = _connectionFactory.CreateConnection();
            return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                sql,
                new { UserId = userId },
                cancellationToken: cancellationToken));
        }

        public async Task<IReadOnlyList<UserAuthenticationAuditLog>> GetByUserIdAsync(Guid userId, int take = 100, CancellationToken cancellationToken = default)
        {
            const string sql = $"""
                {SelectSql}
                WHERE user_id = @UserId
                ORDER BY created_utc DESC
                LIMIT @Take;
                """;

            using var connection = _connectionFactory.CreateConnection();
            var rows = await connection.QueryAsync<UserAuthenticationAuditLog>(new CommandDefinition(
                sql,
                new
                {
                    UserId = userId,
                    Take = take
                },
                cancellationToken: cancellationToken));

            return rows.ToList();
        }

        public async Task<IReadOnlyList<UserAuthenticationAuditLog>> GetPagedAsync(Guid? userId, int skip, int take, CancellationToken cancellationToken = default)
        {
            const string sql = $"""
                {SelectSql}
                WHERE @UserId IS NULL OR user_id = @UserId
                ORDER BY created_utc DESC
                LIMIT @Take OFFSET @Skip;
                """;

            using var connection = _connectionFactory.CreateConnection();
            var rows = await connection.QueryAsync<UserAuthenticationAuditLog>(new CommandDefinition(
                sql,
                new
                {
                    UserId = userId,
                    Skip = skip,
                    Take = take
                },
                cancellationToken: cancellationToken));

            return rows.ToList();
        }

        public async Task<IReadOnlyList<UserAuthenticationAuditLog>> GetFailedAsync(int take = 100, CancellationToken cancellationToken = default)
        {
            const string sql = $"""
                {SelectSql}
                WHERE is_success = 0
                ORDER BY created_utc DESC
                LIMIT @Take;
                """;

            using var connection = _connectionFactory.CreateConnection();
            var rows = await connection.QueryAsync<UserAuthenticationAuditLog>(new CommandDefinition(
                sql,
                new { Take = take },
                cancellationToken: cancellationToken));

            return rows.ToList();
        }

        public async Task<IReadOnlyList<UserAuthenticationAuditLog>> GetRecentAsync(int take = 100, CancellationToken cancellationToken = default)
        {
            const string sql = $"""
                {SelectSql}
                ORDER BY created_utc DESC
                LIMIT @Take;
                """;

            using var connection = _connectionFactory.CreateConnection();
            var rows = await connection.QueryAsync<UserAuthenticationAuditLog>(new CommandDefinition(
                sql,
                new { Take = take },
                cancellationToken: cancellationToken));

            return rows.ToList();
        }

        private const string SelectSql = """
            SELECT
                id AS Id,
                user_id AS UserId,
                user_name AS UserName,
                login AS Login,
                event_type AS EventType,
                status AS Status,
                is_success AS IsSuccess,
                failure_reason AS FailureReason,
                client_ip AS ClientIp,
                ip_source AS IpSource,
                forwarded_for_raw AS ForwardedForRaw,
                real_ip_raw AS RealIpRaw,
                request_path AS RequestPath,
                user_agent AS UserAgent,
                created_utc AS CreatedUtc,
                updated_utc AS UpdatedUtc,
                row_version AS RowVersion
            FROM user_authentication_audit_logs
            """;
    }
}
