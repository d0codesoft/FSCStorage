using Dapper;
using System.Data;

namespace scp.filestorage.Data.Schema
{
    public class DbSchemaRole
    {
        public string Name => "Initial role on Database";

        public async Task<bool> ApplyAsync(
            IDbConnection connection,
            IDbTransaction? transaction,
            ILogger? logger,
            CancellationToken cancellationToken = default)
        {
            try
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    Sql,
                    transaction: transaction,
                    cancellationToken: cancellationToken));

                return true;
            }
            catch (Exception ex)
            {
                if (logger != null)
                {
                    logger.LogError(ex, "Failed to apply default roles to database");
                }
                else
                {
                    Console.Error.WriteLine($"Failed to apply: {Name}");
                    Console.Error.WriteLine(ex.ToString());
                }
            }
            return false;
        }

        private readonly string Sql = """
            INSERT OR IGNORE INTO roles
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
                X'0196A5C21A0070008000000000000001',
                'Administrator',
                'ADMINISTRATOR',
                'Full system administrator role.',
                1,
                strftime('%Y-%m-%dT%H:%M:%fZ','now'),
                NULL,
                randomblob(16)
            );

            INSERT OR IGNORE INTO roles
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
                X'0196A5C21A0070008000000000000002',
                'TenantAdministrator',
                'TENANTADMINISTRATOR',
                'Administrator role limited to a specific tenant.',
                1,
                strftime('%Y-%m-%dT%H:%M:%fZ','now'),
                NULL,
                randomblob(16)
            );

            INSERT OR IGNORE INTO roles
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
                X'0196A5C21A0070008000000000000003',
                'User',
                'USER',
                'Standard authenticated user role.',
                1,
                strftime('%Y-%m-%dT%H:%M:%fZ','now'),
                NULL,
                randomblob(16)
            );

            INSERT OR IGNORE INTO roles
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
                X'0196A5C21A0070008000000000000004',
                'ReadOnly',
                'READONLY',
                'Read-only user role.',
                1,
                strftime('%Y-%m-%dT%H:%M:%fZ','now'),
                NULL,
                randomblob(16)
            );
            """;
    }
}
