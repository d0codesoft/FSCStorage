namespace SCP.StorageFSC.Data.Schema
{
    using Dapper;
    using System.Data;

    public sealed class DbSchemaV3 : DbSchemaBase
    {
        public override int CurrentSchemaVersion => 3;

        public override string Name => "Add background task status tracking";

        protected override string Sql => """
            CREATE TABLE IF NOT EXISTS background_tasks
            (
                id                 BLOB NOT NULL PRIMARY KEY CHECK(length(id) = 16),
                task_id            BLOB NOT NULL CHECK(length(task_id) = 16),
                type               INTEGER NOT NULL,
                status             INTEGER NOT NULL DEFAULT 0,
                upload_id          BLOB NULL CHECK(upload_id IS NULL OR length(upload_id) = 16),
                queued_at_utc      TEXT NOT NULL,
                started_at_utc     TEXT NULL,
                completed_at_utc   TEXT NULL,
                failed_at_utc      TEXT NULL,
                error_message      TEXT NULL,
                result_summary     TEXT NULL,
                created_utc        TEXT NOT NULL
                                   DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
                updated_utc        TEXT NULL,
                row_version        BLOB NOT NULL CHECK(length(row_version) = 16),

                CONSTRAINT uq_background_tasks_task_id UNIQUE (task_id)
            );

            CREATE INDEX IF NOT EXISTS ix_background_tasks_status
                ON background_tasks(status);

            CREATE INDEX IF NOT EXISTS ix_background_tasks_type
                ON background_tasks(type);

            CREATE INDEX IF NOT EXISTS ix_background_tasks_queued_at_utc
                ON background_tasks(queued_at_utc);

            CREATE INDEX IF NOT EXISTS ix_background_tasks_completed_at_utc
                ON background_tasks(completed_at_utc);
            """;

        public override async Task<bool> ApplyAsync(
            IDbConnection connection,
            IDbTransaction? transaction,
            ILogger? logger,
            CancellationToken cancellationToken = default)
        {
            var tableExists = await BackgroundTasksTableExistsAsync(
                connection,
                transaction,
                cancellationToken);

            if (tableExists)
                return true;

            return await base.ApplyAsync(connection, transaction, logger, cancellationToken);
        }

        private static async Task<bool> BackgroundTasksTableExistsAsync(
            IDbConnection connection,
            IDbTransaction? transaction,
            CancellationToken cancellationToken)
        {
            const string sql = """
                SELECT COUNT(1)
                FROM sqlite_master
                WHERE type = 'table'
                  AND name = 'background_tasks';
                """;

            var count = await connection.ExecuteScalarAsync<long>(new CommandDefinition(
                sql,
                transaction: transaction,
                cancellationToken: cancellationToken));

            return count > 0;
        }
    }
}
