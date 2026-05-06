using Dapper;
using Microsoft.Extensions.Logging;
using scp.filestorage.Data.Schema;
using System.Data;
using System.Reflection;

namespace SCP.StorageFSC.Data
{
    public sealed class DbInitializer : IDbInitializer
    {
        private const string SchemaName = "FileStorageService";

        private readonly IDbConnectionFactory _connectionFactory;
        private readonly IReadOnlyList<IDbSchema> _schemas;
        private readonly ILogger<DbInitializer> _logger;

        public DbInitializer(IDbConnectionFactory connectionFactory, ILogger<DbInitializer> logger)
        {
            _connectionFactory = connectionFactory;
            _schemas = LoadSchemasFromCurrentAssembly();
            _logger = logger;
        }

        public async Task InitializeDefaultValuesAsync(CancellationToken cancellationToken = default)
        {
            using var connection = _connectionFactory.CreateConnection();

            try
            {
                connection.Open();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to open database connection.");
                throw;
            }

            try
            {
                var _schemaRole = new DbSchemaRole();
                await _schemaRole.ApplyAsync(connection, null, _logger, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize default values in the database.");
                throw;
            }
        }

        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            using var connection = _connectionFactory.CreateConnection();

            try
            {
                connection.Open();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to open database connection.");
                throw;
            }

            await ApplyPragmasAsync(connection, cancellationToken);

            var metadataTableExists = await DbMetadataTableExistsAsync(connection, cancellationToken);
            _logger.LogInformation("Metadata table exists: {Exists}", metadataTableExists);

            if (!metadataTableExists)
            {
                _logger.LogInformation("Creating metadata table and inserting initial record.");
                await CreateMetadataTableAsync(connection, cancellationToken);
                await InsertInitialMetadataAsync(connection, cancellationToken);
            }

            var currentVersion = await GetCurrentSchemaVersionAsync(connection, cancellationToken);
            _logger.LogInformation("Current database schema version: {Version}", currentVersion);

            var pendingSchemas = _schemas
                .Where(x => x.CurrentSchemaVersion > currentVersion)
                .OrderBy(x => x.CurrentSchemaVersion)
                .ToList();

            if (pendingSchemas.Count == 0)
                return;

            using var transaction = connection.BeginTransaction();

            try
            {
                foreach (var schema in pendingSchemas)
                {
                    _logger.LogInformation(
                        "Applying schema version {Version}: {Name}",
                        schema.CurrentSchemaVersion,
                        schema.Name);
                    var result = await schema.ApplyAsync(connection, transaction, _logger, cancellationToken);

                    if (!result)
                    {
                        _logger.LogError(
                            "Failed to apply schema version {Version}: {Name}. Rolling back transaction.",
                            schema.CurrentSchemaVersion,
                            schema.Name);
                        throw new InvalidOperationException(
                            $"Failed to apply schema version {schema.CurrentSchemaVersion}: {schema.Name}");
                    }
                    else
                    {
                        _logger.LogInformation(
                            "Successfully applied schema version {Version}: {Name}",
                            schema.CurrentSchemaVersion,
                            schema.Name);

                        await UpdateMetadataAsync(
                            connection,
                            transaction,
                            schema.CurrentSchemaVersion,
                            cancellationToken);
                    }
                }
                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        private static IReadOnlyList<IDbSchema> LoadSchemasFromCurrentAssembly()
        {
            var assembly = Assembly.GetExecutingAssembly();

            var schemas = assembly
                .GetTypes()
                .Where(t =>
                    typeof(IDbSchema).IsAssignableFrom(t) &&
                    t is { IsInterface: false, IsAbstract: false } &&
                    t.GetConstructor(Type.EmptyTypes) != null)
                .Select(t => (IDbSchema)Activator.CreateInstance(t)!)
                .OrderBy(t => t.CurrentSchemaVersion)
                .ToList();

            if (schemas.Count == 0)
                throw new InvalidOperationException("No implementation of IDbSchema was found in the current build.");

            var duplicateVersions = schemas
                .GroupBy(x => x.CurrentSchemaVersion)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToArray();

            if (duplicateVersions.Length > 0)
            {
                throw new InvalidOperationException(
                    $"Duplicate versions of the scheme were found: {string.Join(", ", duplicateVersions)}");
            }

            return schemas;
        }

        private static async Task ApplyPragmasAsync(
            IDbConnection connection,
            CancellationToken cancellationToken)
        {
            const string sql = """
                PRAGMA journal_mode = WAL;
                PRAGMA foreign_keys = ON;
                PRAGMA synchronous = NORMAL;
                PRAGMA temp_store = MEMORY;
                PRAGMA cache_size = -20000;
                """;

            await connection.ExecuteAsync(new CommandDefinition(
                sql,
                cancellationToken: cancellationToken));
        }

        private static async Task<bool> DbMetadataTableExistsAsync(
            IDbConnection connection,
            CancellationToken cancellationToken)
        {
            const string sql = """
                SELECT COUNT(1)
                FROM sqlite_master
                WHERE type = 'table'
                  AND name = 'db_metadata';
                """;

            var count = await connection.ExecuteScalarAsync<long>(new CommandDefinition(
                sql,
                cancellationToken: cancellationToken));

            return count > 0;
        }

        private static async Task CreateMetadataTableAsync(
            IDbConnection connection,
            CancellationToken cancellationToken)
        {
            const string sql = """
                CREATE TABLE IF NOT EXISTS db_metadata
                (
                    id                 BLOB PRIMARY KEY,
                    public_id          BLOB    NOT NULL,
                    schema_version     INTEGER NOT NULL,
                    schema_name        TEXT    NOT NULL,
                    created_utc        TEXT    NOT NULL,
                    updated_utc        TEXT    NULL,
                    row_version        BLOB    NOT NULL
                );
                """;

            await connection.ExecuteAsync(new CommandDefinition(
                sql,
                cancellationToken: cancellationToken));
        }

        private static async Task InsertInitialMetadataAsync(
            IDbConnection connection,
            CancellationToken cancellationToken)
        {
            const string sql = """
                INSERT INTO db_metadata
                (
                    id,
                    public_id,
                    schema_version,
                    schema_name,
                    created_utc,
                    updated_utc,
                    row_version
                )
                VALUES
                (
                    @Id,
                    @PublicId,
                    0,
                    @SchemaName,
                    @NowUtc,
                    @NowUtc,
                    @RowVersion
                );
                """;

            var nowUtc = DateTime.UtcNow;
            var id = Guid.CreateVersion7();

            await connection.ExecuteAsync(new CommandDefinition(
                sql,
                new
                {
                    Id = id,
                    PublicId = Guid.CreateVersion7(),
                    SchemaName,
                    NowUtc = nowUtc,
                    RowVersion = Guid.NewGuid()
                },
                cancellationToken: cancellationToken));
        }

        private static async Task<int> GetCurrentSchemaVersionAsync(
            IDbConnection connection,
            CancellationToken cancellationToken)
        {
            const string sql = """
                SELECT schema_version
                FROM db_metadata
                LIMIT 1;
                """;

            var version = await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
                sql,
                cancellationToken: cancellationToken));

            return version ?? 0;
        }

        private static async Task UpdateMetadataAsync(
            IDbConnection connection,
            IDbTransaction transaction,
            int schemaVersion,
            CancellationToken cancellationToken)
        {
            const string sql = """
                UPDATE db_metadata
                SET
                    schema_version = @SchemaVersion,
                    updated_utc = @NowUtc
                WHERE EXISTS (SELECT 1 FROM db_metadata WHERE schema_version IS NOT NULL);
                """;

            await connection.ExecuteAsync(new CommandDefinition(
                sql,
                new
                {
                    SchemaVersion = schemaVersion,
                    NowUtc = DateTime.UtcNow
                },
                transaction: transaction,
                cancellationToken: cancellationToken));
        }
    }
}
