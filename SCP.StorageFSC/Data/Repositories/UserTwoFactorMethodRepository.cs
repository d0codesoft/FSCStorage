using Dapper;
using Microsoft.Data.Sqlite;
using scp.filestorage.Data.Models;
using SCP.StorageFSC.Data;
using System.Data;

namespace scp.filestorage.Data.Repositories
{
    public sealed class UserTwoFactorMethodRepository : IUserTwoFactorMethodRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public UserTwoFactorMethodRepository(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public async Task<bool> InsertAsync(UserTwoFactorMethod method, CancellationToken cancellationToken = default)
        {
            const string sql = """
                INSERT INTO user_two_factor_methods
                (
                    id, user_id, method_type, is_enabled, is_confirmed, is_default,
                    secret_encrypted, destination, masked_destination,
                    confirmed_utc, last_used_utc,
                    failed_attempt_count, last_failed_attempt_utc, locked_until_utc,
                    created_utc, updated_utc, row_version
                )
                VALUES
                (
                    @Id, @UserId, @MethodType, @IsEnabled, @IsConfirmed, @IsDefault,
                    @SecretEncrypted, @Destination, @MaskedDestination,
                    @ConfirmedUtc, @LastUsedUtc,
                    @FailedAttemptCount, @LastFailedAttemptUtc, @LockedUntilUtc,
                    @CreatedUtc, @UpdatedUtc, @RowVersion
                );
                """;

            return await ExecuteAsync(sql, ToDbParams(method), $"Failed to insert 2FA method '{method.Id}'.", cancellationToken);
        }

        public Task<UserTwoFactorMethod?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            const string sql = SelectSql + """

                WHERE id = @Id
                LIMIT 1;
                """;

            return QuerySingleAsync(sql, new { Id = id }, $"Failed to load 2FA method '{id}'.", cancellationToken);
        }

        public async Task<IReadOnlyList<UserTwoFactorMethod>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            const string sql = SelectSql + """

                WHERE user_id = @UserId
                ORDER BY is_default DESC, method_type;
                """;

            return await QueryListAsync(sql, new { UserId = userId }, $"Failed to load 2FA methods for user '{userId}'.", cancellationToken);
        }

        public Task<UserTwoFactorMethod?> GetDefaultAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            const string sql = SelectSql + """

                WHERE user_id = @UserId
                  AND is_default = 1
                  AND is_enabled = 1
                  AND is_confirmed = 1
                LIMIT 1;
                """;

            return QuerySingleAsync(sql, new { UserId = userId }, $"Failed to load default 2FA method for user '{userId}'.", cancellationToken);
        }

        public Task<UserTwoFactorMethod?> GetByUserAndTypeAsync(Guid userId, TwoFactorMethodType methodType, CancellationToken cancellationToken = default)
        {
            const string sql = SelectSql + """

                WHERE user_id = @UserId
                  AND method_type = @MethodType
                LIMIT 1;
                """;

            return QuerySingleAsync(sql, new { UserId = userId, MethodType = (int)methodType }, $"Failed to load 2FA method for user '{userId}'.", cancellationToken);
        }

        public Task<bool> UpdateAsync(UserTwoFactorMethod method, CancellationToken cancellationToken = default)
        {
            const string sql = """
                UPDATE user_two_factor_methods
                SET
                    method_type = @MethodType,
                    is_enabled = @IsEnabled,
                    is_confirmed = @IsConfirmed,
                    is_default = @IsDefault,
                    secret_encrypted = @SecretEncrypted,
                    destination = @Destination,
                    masked_destination = @MaskedDestination,
                    confirmed_utc = @ConfirmedUtc,
                    last_used_utc = @LastUsedUtc,
                    failed_attempt_count = @FailedAttemptCount,
                    last_failed_attempt_utc = @LastFailedAttemptUtc,
                    locked_until_utc = @LockedUntilUtc,
                    updated_utc = @UpdatedUtc,
                    row_version = @RowVersion
                WHERE id = @Id;
                """;

            return ExecuteAsync(sql, ToDbParams(method), $"Failed to update 2FA method '{method.Id}'.", cancellationToken);
        }

        public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            const string sql = "DELETE FROM user_two_factor_methods WHERE id = @Id;";
            return ExecuteAsync(sql, new { Id = id }, $"Failed to delete 2FA method '{id}'.", cancellationToken);
        }

        private async Task<bool> ExecuteAsync(string sql, object parameters, string message, CancellationToken cancellationToken)
        {
            try
            {
                using var connection = _connectionFactory.CreateConnection();
                var affected = await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
                return affected > 0;
            }
            catch (OperationCanceledException) { throw; }
            catch (DataException ex) { throw new RepositoryException($"{message} Data mapping error.", ex); }
            catch (SqliteException ex) { throw new RepositoryException($"{message} Database error.", ex); }
        }

        private async Task<UserTwoFactorMethod?> QuerySingleAsync(string sql, object parameters, string message, CancellationToken cancellationToken)
        {
            try
            {
                using var connection = _connectionFactory.CreateConnection();
                return await connection.QuerySingleOrDefaultAsync<UserTwoFactorMethod>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
            }
            catch (OperationCanceledException) { throw; }
            catch (DataException ex) { throw new RepositoryException($"{message} Data mapping error.", ex); }
            catch (SqliteException ex) { throw new RepositoryException($"{message} Database error.", ex); }
        }

        private async Task<IReadOnlyList<UserTwoFactorMethod>> QueryListAsync(string sql, object parameters, string message, CancellationToken cancellationToken)
        {
            try
            {
                using var connection = _connectionFactory.CreateConnection();
                var rows = await connection.QueryAsync<UserTwoFactorMethod>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
                return rows.ToList();
            }
            catch (OperationCanceledException) { throw; }
            catch (DataException ex) { throw new RepositoryException($"{message} Data mapping error.", ex); }
            catch (SqliteException ex) { throw new RepositoryException($"{message} Database error.", ex); }
        }

        private static object ToDbParams(UserTwoFactorMethod method) => new
        {
            method.Id,
            method.UserId,
            MethodType = (int)method.MethodType,
            IsEnabled = method.IsEnabled ? 1 : 0,
            IsConfirmed = method.IsConfirmed ? 1 : 0,
            IsDefault = method.IsDefault ? 1 : 0,
            method.SecretEncrypted,
            method.Destination,
            method.MaskedDestination,
            method.ConfirmedUtc,
            method.LastUsedUtc,
            method.FailedAttemptCount,
            method.LastFailedAttemptUtc,
            method.LockedUntilUtc,
            method.CreatedUtc,
            method.UpdatedUtc,
            method.RowVersion
        };

        private const string SelectSql = """
            SELECT
                id AS Id,
                user_id AS UserId,
                method_type AS MethodType,
                is_enabled AS IsEnabled,
                is_confirmed AS IsConfirmed,
                is_default AS IsDefault,
                secret_encrypted AS SecretEncrypted,
                destination AS Destination,
                masked_destination AS MaskedDestination,
                confirmed_utc AS ConfirmedUtc,
                last_used_utc AS LastUsedUtc,
                failed_attempt_count AS FailedAttemptCount,
                last_failed_attempt_utc AS LastFailedAttemptUtc,
                locked_until_utc AS LockedUntilUtc,
                created_utc AS CreatedUtc,
                updated_utc AS UpdatedUtc,
                row_version AS RowVersion
            FROM user_two_factor_methods
            """;
    }
}
