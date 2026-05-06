using Dapper;
using Microsoft.Data.Sqlite;
using scp.filestorage.Data.Models;
using SCP.StorageFSC.Data;
using System.Data;

namespace scp.filestorage.Data.Repositories
{
    public sealed class UserLoginChallengeRepository : IUserLoginChallengeRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public UserLoginChallengeRepository(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public async Task<bool> InsertAsync(UserLoginChallenge challenge, CancellationToken cancellationToken = default)
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
                throw new RepositoryException($"Failed to insert user login challenge '{challenge.Id}' due to data mapping error.", ex);
            }
            catch (SqliteException ex)
            {
                throw new RepositoryException($"Failed to insert user login challenge '{challenge.Id}' due to database error.", ex);
            }
        }

        public Task<UserLoginChallenge?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            const string sql = SelectSql + """

                WHERE id = @Id
                LIMIT 1;
                """;

            return QuerySingleAsync(sql, new { Id = id }, $"Failed to load user login challenge '{id}'.", cancellationToken);
        }

        public Task<UserLoginChallenge?> GetByTokenHashAsync(string challengeTokenHash, CancellationToken cancellationToken = default)
        {
            const string sql = SelectSql + """

                WHERE challenge_token_hash = @ChallengeTokenHash
                LIMIT 1;
                """;

            return QuerySingleAsync(
                sql,
                new { ChallengeTokenHash = challengeTokenHash },
                "Failed to load user login challenge by token hash.",
                cancellationToken);
        }

        public async Task<IReadOnlyList<UserLoginChallenge>> GetPendingByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            const string sql = SelectSql + """

                WHERE user_id = @UserId
                  AND completed_utc IS NULL
                  AND expires_utc > @UtcNow
                ORDER BY created_utc DESC;
                """;

            try
            {
                using var connection = _connectionFactory.CreateConnection();

                var rows = await connection.QueryAsync<UserLoginChallenge>(new CommandDefinition(
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
                throw new RepositoryException($"Failed to load pending user login challenges for user '{userId}' due to data mapping error.", ex);
            }
            catch (SqliteException ex)
            {
                throw new RepositoryException($"Failed to load pending user login challenges for user '{userId}' due to database error.", ex);
            }
        }

        public async Task<bool> UpdateAsync(UserLoginChallenge challenge, CancellationToken cancellationToken = default)
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
                throw new RepositoryException($"Failed to update user login challenge '{challenge.Id}' due to data mapping error.", ex);
            }
            catch (SqliteException ex)
            {
                throw new RepositoryException($"Failed to update user login challenge '{challenge.Id}' due to database error.", ex);
            }
        }

        public async Task<bool> DeleteExpiredAsync(DateTime utcNow, CancellationToken cancellationToken = default)
        {
            const string sql = """
                DELETE FROM user_login_challenges
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
                throw new RepositoryException("Failed to delete expired user login challenges due to data mapping error.", ex);
            }
            catch (SqliteException ex)
            {
                throw new RepositoryException("Failed to delete expired user login challenges due to database error.", ex);
            }
        }

        private async Task<UserLoginChallenge?> QuerySingleAsync(
            string sql,
            object parameters,
            string errorMessage,
            CancellationToken cancellationToken)
        {
            try
            {
                using var connection = _connectionFactory.CreateConnection();

                return await connection.QuerySingleOrDefaultAsync<UserLoginChallenge>(new CommandDefinition(
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

        private static object ToDbParams(UserLoginChallenge challenge) => new
        {
            challenge.Id,
            challenge.UserId,
            challenge.ChallengeTokenHash,
            MethodType = (int)challenge.MethodType,
            challenge.ExpiresUtc,
            challenge.CompletedUtc,
            challenge.IpAddress,
            challenge.UserAgent,
            challenge.CreatedUtc,
            challenge.UpdatedUtc,
            challenge.RowVersion
        };

        private const string SelectSql = """
            SELECT
                id AS Id,
                user_id AS UserId,
                challenge_token_hash AS ChallengeTokenHash,
                method_type AS MethodType,
                expires_utc AS ExpiresUtc,
                completed_utc AS CompletedUtc,
                ip_address AS IpAddress,
                user_agent AS UserAgent,
                created_utc AS CreatedUtc,
                updated_utc AS UpdatedUtc,
                row_version AS RowVersion
            FROM user_login_challenges
            """;

        private const string InsertSql = """
            INSERT INTO user_login_challenges
            (
                id,
                user_id,
                challenge_token_hash,
                method_type,
                expires_utc,
                completed_utc,
                ip_address,
                user_agent,
                created_utc,
                updated_utc,
                row_version
            )
            VALUES
            (
                @Id,
                @UserId,
                @ChallengeTokenHash,
                @MethodType,
                @ExpiresUtc,
                @CompletedUtc,
                @IpAddress,
                @UserAgent,
                @CreatedUtc,
                @UpdatedUtc,
                @RowVersion
            );
            """;

        private const string UpdateSql = """
            UPDATE user_login_challenges
            SET
                challenge_token_hash = @ChallengeTokenHash,
                method_type = @MethodType,
                expires_utc = @ExpiresUtc,
                completed_utc = @CompletedUtc,
                ip_address = @IpAddress,
                user_agent = @UserAgent,
                updated_utc = @UpdatedUtc,
                row_version = @RowVersion
            WHERE id = @Id;
            """;
    }
}
