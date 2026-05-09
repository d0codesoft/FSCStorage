using Dapper;
using Microsoft.Data.Sqlite;
using scp.filestorage.Data.Models;
using SCP.StorageFSC.Data;
using System.Data;

namespace scp.filestorage.Data.Repositories
{
    public sealed class UserRoleRepository : IUserRoleRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public UserRoleRepository(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public async Task<bool> InsertAsync(UserRole userRole, CancellationToken cancellationToken = default)
        {
            const string sql = """
                INSERT INTO user_roles
                (
                    id,
                    user_id,
                    role_id,
                    created_utc,
                    updated_utc,
                    row_version
                )
                VALUES
                (
                    @Id,
                    @UserId,
                    @RoleId,
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
                        userRole.Id,
                        userRole.UserId,
                        userRole.RoleId,
                        userRole.CreatedUtc,
                        userRole.UpdatedUtc,
                        userRole.RowVersion
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
                throw new RepositoryException($"Failed to insert user role '{userRole.Id}' due to data mapping error.", ex);
            }
            catch (SqliteException ex)
            {
                throw new RepositoryException($"Failed to insert user role '{userRole.Id}' due to database error.", ex);
            }
        }

        public async Task<UserRole?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            const string sql = UserRoleSelectSql + """

                WHERE id = @Id
                LIMIT 1;
                """;

            try
            {
                using var connection = _connectionFactory.CreateConnection();

                return await connection.QuerySingleOrDefaultAsync<UserRole>(new CommandDefinition(
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
                throw new RepositoryException($"Failed to load user role by id '{id}' due to data mapping error.", ex);
            }
            catch (SqliteException ex)
            {
                throw new RepositoryException($"Failed to load user role by id '{id}' due to database error.", ex);
            }
        }

        public async Task<UserRole?> GetByUserIdAndRoleIdAsync(
            Guid userId,
            Guid roleId,
            CancellationToken cancellationToken = default)
        {
            const string sql = UserRoleSelectSql + """

                WHERE user_id = @UserId
                  AND role_id = @RoleId
                LIMIT 1;
                """;

            try
            {
                using var connection = _connectionFactory.CreateConnection();

                return await connection.QuerySingleOrDefaultAsync<UserRole>(new CommandDefinition(
                    sql,
                    new
                    {
                        UserId = userId,
                        RoleId = roleId
                    },
                    cancellationToken: cancellationToken));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (DataException ex)
            {
                throw new RepositoryException(
                    $"Failed to load user role for user '{userId}' and role '{roleId}' due to data mapping error.",
                    ex);
            }
            catch (SqliteException ex)
            {
                throw new RepositoryException(
                    $"Failed to load user role for user '{userId}' and role '{roleId}' due to database error.",
                    ex);
            }
        }

        public async Task<IReadOnlyList<UserRole>> GetByUserIdAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            const string sql = UserRoleSelectSql + """

                WHERE user_id = @UserId
                ORDER BY created_utc;
                """;

            try
            {
                using var connection = _connectionFactory.CreateConnection();

                var rows = await connection.QueryAsync<UserRole>(new CommandDefinition(
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
                throw new RepositoryException($"Failed to load roles for user '{userId}' due to data mapping error.", ex);
            }
            catch (SqliteException ex)
            {
                throw new RepositoryException($"Failed to load roles for user '{userId}' due to database error.", ex);
            }
        }

        public async Task<IReadOnlyList<Role>> GetRolesByUserIdAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            const string sql = """
                SELECT
                    r.id AS Id,
                    r.name AS Name,
                    r.normalized_name AS NormalizedName,
                    r.description AS Description,
                    r.is_system AS IsSystem,
                    r.created_utc AS CreatedUtc,
                    r.updated_utc AS UpdatedUtc,
                    r.row_version AS RowVersion
                FROM roles r
                INNER JOIN user_roles ur ON ur.role_id = r.id
                WHERE ur.user_id = @UserId
                ORDER BY r.normalized_name;
                """;

            try
            {
                using var connection = _connectionFactory.CreateConnection();

                var rows = await connection.QueryAsync<Role>(new CommandDefinition(
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
                throw new RepositoryException($"Failed to load role details for user '{userId}' due to data mapping error.", ex);
            }
            catch (SqliteException ex)
            {
                throw new RepositoryException($"Failed to load role details for user '{userId}' due to database error.", ex);
            }
        }

        public async Task<bool> UserHasRoleAsync(
            Guid userId,
            Guid roleId,
            CancellationToken cancellationToken = default)
        {
            const string sql = """
                SELECT EXISTS
                (
                    SELECT 1
                    FROM user_roles
                    WHERE user_id = @UserId
                      AND role_id = @RoleId
                );
                """;

            try
            {
                using var connection = _connectionFactory.CreateConnection();

                return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
                    sql,
                    new
                    {
                        UserId = userId,
                        RoleId = roleId
                    },
                    cancellationToken: cancellationToken));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (DataException ex)
            {
                throw new RepositoryException(
                    $"Failed to check role '{roleId}' for user '{userId}' due to data mapping error.",
                    ex);
            }
            catch (SqliteException ex)
            {
                throw new RepositoryException(
                    $"Failed to check role '{roleId}' for user '{userId}' due to database error.",
                    ex);
            }
        }

        public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            const string sql = """
                DELETE FROM user_roles
                WHERE id = @Id;
                """;

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
                throw new RepositoryException($"Failed to delete user role '{id}' due to data mapping error.", ex);
            }
            catch (SqliteException ex)
            {
                throw new RepositoryException($"Failed to delete user role '{id}' due to database error.", ex);
            }
        }

        public async Task<bool> DeleteByUserIdAndRoleIdAsync(
            Guid userId,
            Guid roleId,
            CancellationToken cancellationToken = default)
        {
            const string sql = """
                DELETE FROM user_roles
                WHERE user_id = @UserId
                  AND role_id = @RoleId;
                """;

            try
            {
                using var connection = _connectionFactory.CreateConnection();

                var affected = await connection.ExecuteAsync(new CommandDefinition(
                    sql,
                    new
                    {
                        UserId = userId,
                        RoleId = roleId
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
                throw new RepositoryException(
                    $"Failed to delete role '{roleId}' from user '{userId}' due to data mapping error.",
                    ex);
            }
            catch (SqliteException ex)
            {
                throw new RepositoryException(
                    $"Failed to delete role '{roleId}' from user '{userId}' due to database error.",
                    ex);
            }
        }

        public async Task<int> DeleteByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            const string sql = """
                DELETE FROM user_roles
                WHERE user_id = @UserId;
                """;

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
                throw new RepositoryException($"Failed to delete roles for user '{userId}' due to data mapping error.", ex);
            }
            catch (SqliteException ex)
            {
                throw new RepositoryException($"Failed to delete roles for user '{userId}' due to database error.", ex);
            }
        }

        private const string UserRoleSelectSql = """
            SELECT
                id AS Id,
                user_id AS UserId,
                role_id AS RoleId,
                created_utc AS CreatedUtc,
                updated_utc AS UpdatedUtc,
                row_version AS RowVersion
            FROM user_roles
            """;
    }
}
