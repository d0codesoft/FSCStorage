using Dapper;
using Microsoft.Data.Sqlite;
using SCP.StorageFSC.Data.Models;
using System.Data;

namespace SCP.StorageFSC.Data.Repositories
{
    public sealed class SystemSettingRepository : ISystemSettingRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public SystemSettingRepository(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public async Task<IReadOnlyList<SystemSetting>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            const string sql = SelectBaseSql + """

                ORDER BY name;
                """;

            try
            {
                using var connection = _connectionFactory.CreateConnection();
                var settings = await connection.QueryAsync<SystemSetting>(new CommandDefinition(
                    sql,
                    cancellationToken: cancellationToken));

                return settings.AsList();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (DataException ex)
            {
                throw new RepositoryException("Failed to load system settings due to data mapping error.", ex);
            }
            catch (SqliteException ex)
            {
                throw new RepositoryException("Failed to load system settings due to database error.", ex);
            }
        }

        public async Task<SystemSetting?> GetByNameAsync(
            string name,
            CancellationToken cancellationToken = default)
        {
            const string sql = SelectBaseSql + """

                WHERE name = @Name
                LIMIT 1;
                """;

            try
            {
                using var connection = _connectionFactory.CreateConnection();
                return await connection.QuerySingleOrDefaultAsync<SystemSetting>(new CommandDefinition(
                    sql,
                    new { Name = name },
                    cancellationToken: cancellationToken));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (DataException ex)
            {
                throw new RepositoryException($"Failed to load system setting '{name}' due to data mapping error.", ex);
            }
            catch (SqliteException ex)
            {
                throw new RepositoryException($"Failed to load system setting '{name}' due to database error.", ex);
            }
        }

        public async Task UpsertAsync(SystemSetting setting, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(setting);

            const string sql = """
                INSERT INTO system_settings
                (
                    id,
                    name,
                    value,
                    description,
                    created_utc,
                    updated_utc,
                    row_version
                )
                VALUES
                (
                    @Id,
                    @Name,
                    @Value,
                    @Description,
                    @CreatedUtc,
                    @UpdatedUtc,
                    @RowVersion
                )
                ON CONFLICT(name) DO UPDATE SET
                    value = excluded.value,
                    description = excluded.description,
                    updated_utc = @NowUtc,
                    row_version = @NewRowVersion;
                """;

            try
            {
                using var connection = _connectionFactory.CreateConnection();
                await connection.ExecuteAsync(new CommandDefinition(
                    sql,
                    new
                    {
                        setting.Id,
                        setting.Name,
                        setting.Value,
                        setting.Description,
                        setting.CreatedUtc,
                        setting.UpdatedUtc,
                        setting.RowVersion,
                        NowUtc = DateTime.UtcNow,
                        NewRowVersion = Guid.NewGuid()
                    },
                    cancellationToken: cancellationToken));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (DataException ex)
            {
                throw new RepositoryException($"Failed to save system setting '{setting.Name}' due to data mapping error.", ex);
            }
            catch (SqliteException ex)
            {
                throw new RepositoryException($"Failed to save system setting '{setting.Name}' due to database error.", ex);
            }
        }

        private const string SelectBaseSql = """
            SELECT
                id AS Id,
                name AS Name,
                value AS Value,
                description AS Description,
                created_utc AS CreatedUtc,
                updated_utc AS UpdatedUtc,
                row_version AS RowVersion
            FROM system_settings
            """;
    }
}
