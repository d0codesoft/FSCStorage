using Dapper;
using Microsoft.Data.Sqlite;
using scp.filestorage.Data.Models;
using SCP.StorageFSC.Data;
using System.Data;

namespace scp.filestorage.Data.Repositories
{
    public sealed class UserRecoveryCodeRepository : IUserRecoveryCodeRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public UserRecoveryCodeRepository(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public async Task<bool> InsertAsync(UserRecoveryCode code, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(code);

            try
            {
                using var connection = _connectionFactory.CreateConnection();

                var affected = await connection.ExecuteAsync(new CommandDefinition(
                    InsertSql,
                    ToDbParams(code),
                    cancellationToken: cancellationToken));

                return affected > 0;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (DataException ex)
            {
                throw new RepositoryException($"Failed to insert recovery code '{code.Id}' due to data mapping error.", ex);
            }
            catch (SqliteException ex)
            {
                throw new RepositoryException($"Failed to insert recovery code '{code.Id}' due to database error.", ex);
            }
        }

        public async Task<int> InsertManyAsync(IEnumerable<UserRecoveryCode> codes, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(codes);

            try
            {
                using var connection = _connectionFactory.CreateConnection();

                var affected = await connection.ExecuteAsync(new CommandDefinition(
                    InsertSql,
                    codes.Select(ToDbParams).ToArray(),
                    cancellationToken: cancellationToken));

                return affected;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (DataException ex)
            {
                throw new RepositoryException("Failed to insert recovery codes due to data mapping error.", ex);
            }
            catch (SqliteException ex)
            {
                throw new RepositoryException("Failed to insert recovery codes due to database error.", ex);
            }
        }

        public async Task<IReadOnlyList<UserRecoveryCode>> GetUnusedByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            const string sql = SelectSql + """

                WHERE user_id = @UserId
                  AND is_used = 0
                ORDER BY created_utc DESC;
                """;

            try
            {
                using var connection = _connectionFactory.CreateConnection();

                var rows = await connection.QueryAsync<UserRecoveryCode>(new CommandDefinition(
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
                throw new RepositoryException($"Failed to load unused recovery codes for user '{userId}' due to data mapping error.", ex);
            }
            catch (SqliteException ex)
            {
                throw new RepositoryException($"Failed to load unused recovery codes for user '{userId}' due to database error.", ex);
            }
        }

        public Task<UserRecoveryCode?> GetUnusedByHashAsync(Guid userId, string codeHash, CancellationToken cancellationToken = default)
        {
            const string sql = SelectSql + """

                WHERE user_id = @UserId
                  AND code_hash = @CodeHash
                  AND is_used = 0
                LIMIT 1;
                """;

            return QuerySingleAsync(
                sql,
                new
                {
                    UserId = userId,
                    CodeHash = codeHash
                },
                $"Failed to load unused recovery code for user '{userId}'.",
                cancellationToken);
        }

        public async Task<bool> MarkUsedAsync(Guid id, DateTime usedUtc, string? ipAddress, string? userAgent, CancellationToken cancellationToken = default)
        {
            try
            {
                using var connection = _connectionFactory.CreateConnection();

                var affected = await connection.ExecuteAsync(new CommandDefinition(
                    MarkUsedSql,
                    new
                    {
                        Id = id,
                        UsedUtc = usedUtc,
                        UsedIpAddress = ipAddress,
                        UsedUserAgent = userAgent,
                        UpdatedUtc = usedUtc,
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
                throw new RepositoryException($"Failed to mark recovery code '{id}' as used due to data mapping error.", ex);
            }
            catch (SqliteException ex)
            {
                throw new RepositoryException($"Failed to mark recovery code '{id}' as used due to database error.", ex);
            }
        }

        public async Task<int> DeleteByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            const string sql = "DELETE FROM user_recovery_codes WHERE user_id = @UserId;";

            try
            {
                using var connection = _connectionFactory.CreateConnection();

                return await connection.ExecuteAsync(new CommandDefinition(
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
                throw new RepositoryException($"Failed to delete recovery codes for user '{userId}' due to data mapping error.", ex);
            }
            catch (SqliteException ex)
            {
                throw new RepositoryException($"Failed to delete recovery codes for user '{userId}' due to database error.", ex);
            }
        }

        private async Task<UserRecoveryCode?> QuerySingleAsync(
            string sql,
            object parameters,
            string errorMessage,
            CancellationToken cancellationToken)
        {
            try
            {
                using var connection = _connectionFactory.CreateConnection();

                return await connection.QuerySingleOrDefaultAsync<UserRecoveryCode>(new CommandDefinition(
                    sql,
                    parameters,
                    cancellationToken: cancellationToken));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (DataException ex)
            {
                throw new RepositoryException($"{errorMessage} Data mapping error.", ex);
            }
            catch (SqliteException ex)
            {
                throw new RepositoryException($"{errorMessage} Database error.", ex);
            }
        }

        private static object ToDbParams(UserRecoveryCode code) => new
        {
            code.Id,
            code.UserId,
            code.CodeHash,
            IsUsed = code.IsUsed ? 1 : 0,
            code.UsedUtc,
            code.UsedIpAddress,
            code.CreatedUtc,
            code.UpdatedUtc,
            code.RowVersion
        };

        private const string SelectSql = """
            SELECT
                id AS Id,
                user_id AS UserId,
                code_hash AS CodeHash,
                is_used AS IsUsed,
                used_utc AS UsedUtc,
                used_ip_address AS UsedIpAddress,
                created_utc AS CreatedUtc,
                updated_utc AS UpdatedUtc,
                row_version AS RowVersion
            FROM user_recovery_codes
            """;

        private const string InsertSql = """
            INSERT INTO user_recovery_codes
            (
                id,
                user_id,
                code_hash,
                is_used,
                used_utc,
                used_ip_address,
                created_utc,
                updated_utc,
                row_version
            )
            VALUES
            (
                @Id,
                @UserId,
                @CodeHash,
                @IsUsed,
                @UsedUtc,
                @UsedIpAddress,
                @CreatedUtc,
                @UpdatedUtc,
                @RowVersion
            );
            """;

        private const string MarkUsedSql = """
            UPDATE user_recovery_codes
            SET
                is_used = 1,
                used_utc = @UsedUtc,
                used_ip_address = @UsedIpAddress,
                used_user_agent = @UsedUserAgent,
                updated_utc = @UpdatedUtc,
                row_version = @RowVersion
            WHERE id = @Id
              AND is_used = 0;
            """;
    }
}
