using Dapper;
using Microsoft.Data.Sqlite;
using SCP.StorageFSC.Data;
using SCP.StorageFSC.Data.Models;
using System.Data;

namespace scp.filestorage.Data.Repositories
{
    public sealed class UserRepository : IUserRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public UserRepository(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public async Task<bool> InsertAsync(User user, CancellationToken cancellationToken = default)
        {
            const string sql = """
                INSERT INTO users
                (
                    id,
                    name,
                    normalized_name,
                    email,
                    normalized_email,
                    email_confirmed,
                    phone_number,
                    phone_number_confirmed,
                    password_hash,
                    password_changed_utc,
                    security_stamp,
                    is_active,
                    is_locked,
                    locked_until_utc,
                    failed_login_count,
                    last_failed_login_utc,
                    last_login_utc,
                    last_login_ip_address,
                    two_factor_enabled,
                    two_factor_required_for_every_login,
                    preferred_two_factor_method,
                    two_factor_enabled_utc,
                    two_factor_last_used_utc,
                    must_change_password,
                    password_expires_utc,
                    external_user_id,
                    comment,
                    created_utc,
                    updated_utc,
                    row_version
                )
                VALUES
                (
                    @Id,
                    @Name,
                    @NormalizedName,
                    @Email,
                    @NormalizedEmail,
                    @EmailConfirmed,
                    @PhoneNumber,
                    @PhoneNumberConfirmed,
                    @PasswordHash,
                    @PasswordChangedUtc,
                    @SecurityStamp,
                    @IsActive,
                    @IsLocked,
                    @LockedUntilUtc,
                    @FailedLoginCount,
                    @LastFailedLoginUtc,
                    @LastLoginUtc,
                    @LastLoginIpAddress,
                    @TwoFactorEnabled,
                    @TwoFactorRequiredForEveryLogin,
                    @PreferredTwoFactorMethod,
                    @TwoFactorEnabledUtc,
                    @TwoFactorLastUsedUtc,
                    @MustChangePassword,
                    @PasswordExpiresUtc,
                    @ExternalUserId,
                    @Comment,
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
                    ToDbParams(user),
                    cancellationToken: cancellationToken));

                return affected > 0;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (DataException ex)
            {
                throw new RepositoryException($"Failed to insert user '{user.Id}' due to data mapping error.", ex);
            }
            catch (SqliteException ex)
            {
                throw new RepositoryException($"Failed to insert user '{user.Id}' due to database error.", ex);
            }
        }

        public async Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            const string sql = UserSelectSql + """

                WHERE id = @Id
                LIMIT 1;
                """;

            return await QuerySingleAsync(sql, new { Id = id }, $"Failed to load user by id '{id}'.", cancellationToken);
        }

        public async Task<User?> GetByNormalizedNameAsync(string normalizedName, CancellationToken cancellationToken = default)
        {
            const string sql = UserSelectSql + """

                WHERE normalized_name = @NormalizedName
                LIMIT 1;
                """;

            return await QuerySingleAsync(sql, new { NormalizedName = normalizedName }, $"Failed to load user by normalized name '{normalizedName}'.", cancellationToken);
        }

        public async Task<User?> GetByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default)
        {
            const string sql = UserSelectSql + """

                WHERE normalized_email = @NormalizedEmail
                LIMIT 1;
                """;

            return await QuerySingleAsync(sql, new { NormalizedEmail = normalizedEmail }, $"Failed to load user by normalized email '{normalizedEmail}'.", cancellationToken);
        }

        public async Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            const string sql = UserSelectSql + """

                ORDER BY normalized_name;
                """;

            try
            {
                using var connection = _connectionFactory.CreateConnection();
                var rows = await connection.QueryAsync<User>(new CommandDefinition(sql, cancellationToken: cancellationToken));
                return rows.ToList();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (DataException ex)
            {
                throw new RepositoryException("Failed to load users due to data mapping error.", ex);
            }
            catch (SqliteException ex)
            {
                throw new RepositoryException("Failed to load users due to database error.", ex);
            }
        }

        public async Task<bool> UpdateAsync(User user, CancellationToken cancellationToken = default)
        {
            const string sql = """
                UPDATE users
                SET
                    name = @Name,
                    normalized_name = @NormalizedName,
                    email = @Email,
                    normalized_email = @NormalizedEmail,
                    email_confirmed = @EmailConfirmed,
                    phone_number = @PhoneNumber,
                    phone_number_confirmed = @PhoneNumberConfirmed,
                    password_hash = @PasswordHash,
                    password_changed_utc = @PasswordChangedUtc,
                    security_stamp = @SecurityStamp,
                    is_active = @IsActive,
                    is_locked = @IsLocked,
                    locked_until_utc = @LockedUntilUtc,
                    failed_login_count = @FailedLoginCount,
                    last_failed_login_utc = @LastFailedLoginUtc,
                    last_login_utc = @LastLoginUtc,
                    last_login_ip_address = @LastLoginIpAddress,
                    two_factor_enabled = @TwoFactorEnabled,
                    two_factor_required_for_every_login = @TwoFactorRequiredForEveryLogin,
                    preferred_two_factor_method = @PreferredTwoFactorMethod,
                    two_factor_enabled_utc = @TwoFactorEnabledUtc,
                    two_factor_last_used_utc = @TwoFactorLastUsedUtc,
                    must_change_password = @MustChangePassword,
                    password_expires_utc = @PasswordExpiresUtc,
                    external_user_id = @ExternalUserId,
                    comment = @Comment,
                    updated_utc = @UpdatedUtc,
                    row_version = @RowVersion
                WHERE id = @Id;
                """;

            try
            {
                using var connection = _connectionFactory.CreateConnection();

                var affected = await connection.ExecuteAsync(new CommandDefinition(
                    sql,
                    ToDbParams(user),
                    cancellationToken: cancellationToken));

                return affected > 0;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (DataException ex)
            {
                throw new RepositoryException($"Failed to update user '{user.Id}' due to data mapping error.", ex);
            }
            catch (SqliteException ex)
            {
                throw new RepositoryException($"Failed to update user '{user.Id}' due to database error.", ex);
            }
        }

        public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            const string sql = "DELETE FROM users WHERE id = @Id;";

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
                throw new RepositoryException($"Failed to delete user '{id}' due to data mapping error.", ex);
            }
            catch (SqliteException ex)
            {
                throw new RepositoryException($"Failed to delete user '{id}' due to database error.", ex);
            }
        }

        private async Task<User?> QuerySingleAsync(
            string sql,
            object parameters,
            string errorMessage,
            CancellationToken cancellationToken)
        {
            try
            {
                using var connection = _connectionFactory.CreateConnection();

                return await connection.QuerySingleOrDefaultAsync<User>(new CommandDefinition(
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

        private static object ToDbParams(User user)
        {
            return new
            {
                user.Id,
                user.Name,
                user.NormalizedName,
                user.Email,
                user.NormalizedEmail,
                EmailConfirmed = user.EmailConfirmed ? 1 : 0,
                user.PhoneNumber,
                PhoneNumberConfirmed = user.PhoneNumberConfirmed ? 1 : 0,
                user.PasswordHash,
                user.PasswordChangedUtc,
                user.SecurityStamp,
                IsActive = user.IsActive ? 1 : 0,
                IsLocked = user.IsLocked ? 1 : 0,
                user.LockedUntilUtc,
                user.FailedLoginCount,
                user.LastFailedLoginUtc,
                user.LastLoginUtc,
                user.LastLoginIpAddress,
                TwoFactorEnabled = user.TwoFactorEnabled ? 1 : 0,
                TwoFactorRequiredForEveryLogin = user.TwoFactorRequiredForEveryLogin ? 1 : 0,
                PreferredTwoFactorMethod = (int)user.PreferredTwoFactorMethod,
                user.TwoFactorEnabledUtc,
                user.TwoFactorLastUsedUtc,
                MustChangePassword = user.MustChangePassword ? 1 : 0,
                user.PasswordExpiresUtc,
                user.ExternalUserId,
                user.Comment,
                user.CreatedUtc,
                user.UpdatedUtc,
                user.RowVersion
            };
        }

        private const string UserSelectSql = """
            SELECT
                id AS Id,
                name AS Name,
                normalized_name AS NormalizedName,
                email AS Email,
                normalized_email AS NormalizedEmail,
                email_confirmed AS EmailConfirmed,
                phone_number AS PhoneNumber,
                phone_number_confirmed AS PhoneNumberConfirmed,
                password_hash AS PasswordHash,
                password_changed_utc AS PasswordChangedUtc,
                security_stamp AS SecurityStamp,
                is_active AS IsActive,
                is_locked AS IsLocked,
                locked_until_utc AS LockedUntilUtc,
                failed_login_count AS FailedLoginCount,
                last_failed_login_utc AS LastFailedLoginUtc,
                last_login_utc AS LastLoginUtc,
                last_login_ip_address AS LastLoginIpAddress,
                two_factor_enabled AS TwoFactorEnabled,
                two_factor_required_for_every_login AS TwoFactorRequiredForEveryLogin,
                preferred_two_factor_method AS PreferredTwoFactorMethod,
                two_factor_enabled_utc AS TwoFactorEnabledUtc,
                two_factor_last_used_utc AS TwoFactorLastUsedUtc,
                must_change_password AS MustChangePassword,
                password_expires_utc AS PasswordExpiresUtc,
                external_user_id AS ExternalUserId,
                comment AS Comment,
                created_utc AS CreatedUtc,
                updated_utc AS UpdatedUtc,
                row_version AS RowVersion
            FROM users
            """;
    }
}
