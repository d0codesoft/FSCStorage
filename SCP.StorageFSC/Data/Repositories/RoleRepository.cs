using Dapper;
using Microsoft.Data.Sqlite;
using scp.filestorage.Data.Models;
using SCP.StorageFSC.Data;
using System.Data;

namespace scp.filestorage.Data.Repositories
{
    public sealed class RoleRepository : IRoleRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public RoleRepository(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public async Task<bool> InsertAsync(Role role, CancellationToken cancellationToken = default)
        {
            const string sql = """
                INSERT INTO roles
                (
                    id,
                    name,
                    normalized_name,
                    description,
                    is_system,
                    created_utc,
                    updated_utc,
                    row_version
                )
                VALUES
                (
                    @Id,
                    @Name,
                    @NormalizedName,
                    @Description,
                    @IsSystem,
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
                        role.Id,
                        role.Name,
                        role.NormalizedName,
                        role.Description,
                        IsSystem = role.IsSystem ? 1 : 0,
                        role.CreatedUtc,
                        role.UpdatedUtc,
                        role.RowVersion
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
                throw new RepositoryException($"Failed to insert role '{role.Id}' due to data mapping error.", ex);
            }
            catch (SqliteException ex)
            {
                throw new RepositoryException($"Failed to insert role '{role.Id}' due to database error.", ex);
            }
        }

        public async Task<Role?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            const string sql = RoleSelectSql + """
                WHERE id = @Id
                LIMIT 1;
                """;

            try
            {
                using var connection = _connectionFactory.CreateConnection();

                return await connection.QuerySingleOrDefaultAsync<Role>(new CommandDefinition(
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
                throw new RepositoryException($"Failed to load role by id '{id}' due to data mapping error.", ex);
            }
            catch (SqliteException ex)
            {
                throw new RepositoryException($"Failed to load role by id '{id}' due to database error.", ex);
            }
        }

        public async Task<Role?> GetByNormalizedNameAsync(
            string normalizedName,
            CancellationToken cancellationToken = default)
        {
            const string sql = RoleSelectSql + """
                WHERE normalized_name = @NormalizedName
                LIMIT 1;
                """;

            try
            {
                using var connection = _connectionFactory.CreateConnection();

                return await connection.QuerySingleOrDefaultAsync<Role>(new CommandDefinition(
                    sql,
                    new { NormalizedName = normalizedName },
                    cancellationToken: cancellationToken));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (DataException ex)
            {
                throw new RepositoryException(
                    $"Failed to load role by normalized name '{normalizedName}' due to data mapping error.",
                    ex);
            }
            catch (SqliteException ex)
            {
                throw new RepositoryException(
                    $"Failed to load role by normalized name '{normalizedName}' due to database error.",
                    ex);
            }
        }

        public async Task<IReadOnlyList<Role>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            const string sql = RoleSelectSql + """
                ORDER BY normalized_name;
                """;

            try
            {
                using var connection = _connectionFactory.CreateConnection();

                var rows = await connection.QueryAsync<Role>(new CommandDefinition(
                    sql,
                    cancellationToken: cancellationToken));

                return rows.ToList();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (DataException ex)
            {
                throw new RepositoryException("Failed to load roles due to data mapping error.", ex);
            }
            catch (SqliteException ex)
            {
                throw new RepositoryException("Failed to load roles due to database error.", ex);
            }
        }

        public async Task<IReadOnlyList<Role>> GetSystemRolesAsync(CancellationToken cancellationToken = default)
        {
            const string sql = RoleSelectSql + """
                WHERE is_system = 1
                ORDER BY normalized_name;
                """;

            try
            {
                using var connection = _connectionFactory.CreateConnection();

                var rows = await connection.QueryAsync<Role>(new CommandDefinition(
                    sql,
                    cancellationToken: cancellationToken));

                return rows.ToList();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (DataException ex)
            {
                throw new RepositoryException("Failed to load system roles due to data mapping error.", ex);
            }
            catch (SqliteException ex)
            {
                throw new RepositoryException("Failed to load system roles due to database error.", ex);
            }
        }

        public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
        {
            const string sql = """
                SELECT EXISTS
                (
                    SELECT 1
                    FROM roles
                    WHERE id = @Id
                );
                """;

            try
            {
                using var connection = _connectionFactory.CreateConnection();

                return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
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
                throw new RepositoryException($"Failed to check role existence '{id}' due to data mapping error.", ex);
            }
            catch (SqliteException ex)
            {
                throw new RepositoryException($"Failed to check role existence '{id}' due to database error.", ex);
            }
        }

        public async Task<bool> UpdateAsync(Role role, CancellationToken cancellationToken = default)
        {
            const string sql = """
                UPDATE roles
                SET
                    name = @Name,
                    normalized_name = @NormalizedName,
                    description = @Description,
                    is_system = @IsSystem,
                    updated_utc = @UpdatedUtc,
                    row_version = @RowVersion
                WHERE id = @Id;
                """;

            try
            {
                using var connection = _connectionFactory.CreateConnection();

                var affected = await connection.ExecuteAsync(new CommandDefinition(
                    sql,
                    new
                    {
                        role.Id,
                        role.Name,
                        role.NormalizedName,
                        role.Description,
                        IsSystem = role.IsSystem ? 1 : 0,
                        role.UpdatedUtc,
                        role.RowVersion
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
                throw new RepositoryException($"Failed to update role '{role.Id}' due to data mapping error.", ex);
            }
            catch (SqliteException ex)
            {
                throw new RepositoryException($"Failed to update role '{role.Id}' due to database error.", ex);
            }
        }

        public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            const string sql = """
                DELETE FROM roles
                WHERE id = @Id
                  AND is_system = 0;
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
                throw new RepositoryException($"Failed to delete role '{id}' due to data mapping error.", ex);
            }
            catch (SqliteException ex)
            {
                throw new RepositoryException($"Failed to delete role '{id}' due to database error.", ex);
            }
        }

        private const string RoleSelectSql = """
            SELECT
                id AS Id,
                name AS Name,
                normalized_name AS NormalizedName,
                description AS Description,
                is_system AS IsSystem,
                created_utc AS CreatedUtc,
                updated_utc AS UpdatedUtc,
                row_version AS RowVersion
            FROM roles
            """;
    }
}
