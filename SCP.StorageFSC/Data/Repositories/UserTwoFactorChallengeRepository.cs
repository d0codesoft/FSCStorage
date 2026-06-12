using Dapper;
using Microsoft.Data.Sqlite;
using scp.filestorage.Data.Models;
using SCP.StorageFSC.Data;
using System.Data;

namespace scp.filestorage.Data.Repositories
{
    public sealed class UserTwoFactorChallengeRepository : IUserTwoFactorChallengeRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public UserTwoFactorChallengeRepository(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public async Task<bool> InsertAsync(UserTwoFactorChallenge challenge, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(challenge);

            try
            {
                using var connection = _connectionFactory.CreateConnection();

                var affected = await connection.ExecuteAsync(new CommandDefinition(
                    InsertSql,
                    ToDbParams(challenge),
                    cancellationToken: cancellationToken));

                return affected > 0;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (DataException ex)
            {
                throw new RepositoryException($"Failed to insert 2FA challenge '{challenge.Id}' due to data mapping error.", ex);
            }
            catch (SqliteException ex)
            {
                throw new RepositoryException($"Failed to insert 2FA challenge '{challenge.Id}' due to database error.", ex);
            }
        }

        public Task<UserTwoFactorChallenge?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            const string sql = SelectSql + """

                WHERE id = @Id
                LIMIT 1;
                """;

            return QuerySingleAsync(sql, new { Id = id }, $"Failed to load 2FA challenge '{id}'.", cancellationToken);
        }

        public async Task<IReadOnlyList<UserTwoFactorChallenge>> GetPendingByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            const string sql = SelectSql + """

                WHERE user_id = @UserId
                  AND verified_utc IS NULL
                  AND expires_utc > @UtcNow
                ORDER BY created_utc DESC;
                """;

            try
            {
                using var connection = _connectionFactory.CreateConnection();

                var rows = await connection.QueryAsync<UserTwoFactorChallenge>(new CommandDefinition(
                    sql,
                    new
                    {
                        UserId = userId,
                        UtcNow = DateTime.UtcNow
                    },
                    cancellationToken: cancellationToken));

                return rows.ToList();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (DataException ex)
            {
                throw new RepositoryException($"Failed to load pending 2FA challenges for user '{userId}' due to data mapping error.", ex);
            }
            catch (SqliteException ex)
            {
                throw new RepositoryException($"Failed to load pending 2FA challenges for user '{userId}' due to database error.", ex);
            }
        }

        public async Task<bool> UpdateAsync(UserTwoFactorChallenge challenge, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(challenge);

            try
            {
                using var connection = _connectionFactory.CreateConnection();

                var affected = await connection.ExecuteAsync(new CommandDefinition(
                    UpdateSql,
                    ToDbParams(challenge),
                    cancellationToken: cancellationToken));

                return affected > 0;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (DataException ex)
            {
                throw new RepositoryException($"Failed to update 2FA challenge '{challenge.Id}' due to data mapping error.", ex);
            }
            catch (SqliteException ex)
            {
                throw new RepositoryException($"Failed to update 2FA challenge '{challenge.Id}' due to database error.", ex);
            }
        }

        public async Task<bool> DeleteExpiredAsync(DateTime utcNow, CancellationToken cancellationToken = default)
        {
            const string sql = """
                DELETE FROM user_two_factor_challenges
                WHERE expires_utc <= @UtcNow;
                """;

            try
            {
                using var connection = _connectionFactory.CreateConnection();

                var affected = await connection.ExecuteAsync(new CommandDefinition(
                    sql,
                    new { UtcNow = utcNow },
                    cancellationToken: cancellationToken));

                return affected > 0;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (DataException ex)
            {
                throw new RepositoryException("Failed to delete expired 2FA challenges due to data mapping error.", ex);
            }
            catch (SqliteException ex)
            {
                throw new RepositoryException("Failed to delete expired 2FA challenges due to database error.", ex);
            }
        }

        private async Task<UserTwoFactorChallenge?> QuerySingleAsync(
            string sql,
            object parameters,
            string errorMessage,
            CancellationToken cancellationToken)
        {
            try
            {
                using var connection = _connectionFactory.CreateConnection();

                return await connection.QuerySingleOrDefaultAsync<UserTwoFactorChallenge>(new CommandDefinition(
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

        private static object ToDbParams(UserTwoFactorChallenge challenge) => new
        {
            challenge.Id,
            challenge.UserId,
            challenge.UserTwoFactorMethodId,
            MethodType = (int)challenge.MethodType,
            challenge.CodeHash,
            challenge.Destination,
            Status = (int)challenge.Status,
            challenge.ExpiresUtc,
            challenge.VerifiedUtc,
            challenge.FailedAttemptCount,
            challenge.MaxFailedAttemptCount,
            challenge.CreatedIpAddress,
            challenge.VerifiedIpAddress,
            challenge.UserAgent,
            challenge.CreatedUtc,
            challenge.UpdatedUtc,
            challenge.RowVersion
        };

        private const string SelectSql = """
            SELECT
                id AS Id,
                user_id AS UserId,
                user_two_factor_method_id AS UserTwoFactorMethodId,
                method_type AS MethodType,
                code_hash AS CodeHash,
                destination AS Destination,
                status AS Status,
                expires_utc AS ExpiresUtc,
                verified_utc AS VerifiedUtc,
                failed_attempt_count AS FailedAttemptCount,
                max_failed_attempt_count AS MaxFailedAttemptCount,
                created_ip_address AS CreatedIpAddress,
                verified_ip_address AS VerifiedIpAddress,
                user_agent AS UserAgent,
                created_utc AS CreatedUtc,
                updated_utc AS UpdatedUtc,
                row_version AS RowVersion
            FROM user_two_factor_challenges
            """;

        private const string InsertSql = """
            INSERT INTO user_two_factor_challenges
            (
                id,
                user_id,
                user_two_factor_method_id,
                method_type,
                code_hash,
                destination,
                status,
                expires_utc,
                verified_utc,
                failed_attempt_count,
                max_failed_attempt_count,
                created_ip_address,
                verified_ip_address,
                user_agent,
                created_utc,
                updated_utc,
                row_version
            )
            VALUES
            (
                @Id,
                @UserId,
                @UserTwoFactorMethodId,
                @MethodType,
                @CodeHash,
                @Destination,
                @Status,
                @ExpiresUtc,
                @VerifiedUtc,
                @FailedAttemptCount,
                @MaxFailedAttemptCount,
                @CreatedIpAddress,
                @VerifiedIpAddress,
                @UserAgent,
                @CreatedUtc,
                @UpdatedUtc,
                @RowVersion
            );
            """;

        private const string UpdateSql = """
            UPDATE user_two_factor_challenges
            SET
                method_type = @MethodType,
                code_hash = @CodeHash,
                destination = @Destination,
                status = @Status,
                expires_utc = @ExpiresUtc,
                verified_utc = @VerifiedUtc,
                failed_attempt_count = @FailedAttemptCount,
                max_failed_attempt_count = @MaxFailedAttemptCount,
                created_ip_address = @CreatedIpAddress,
                verified_ip_address = @VerifiedIpAddress,
                user_agent = @UserAgent,
                updated_utc = @UpdatedUtc,
                row_version = @RowVersion
            WHERE id = @Id;
            """;
    }
}
