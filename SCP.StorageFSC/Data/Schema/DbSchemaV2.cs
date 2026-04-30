namespace SCP.StorageFSC.Data.Schema
{
    using Dapper;
    using System.Data;

    public sealed class DbSchemaV2 : DbSchemaBase
    {
        public override int CurrentSchemaVersion => 2;

        public override string Name => "Add stored file compression state";

        protected override string Sql => """
            ALTER TABLE stored_files
                ADD COLUMN filestore_state_compress INTEGER NOT NULL DEFAULT 0;
            """;

        public override async Task<bool> ApplyAsync(
            IDbConnection connection,
            IDbTransaction? transaction,
            ILogger? logger,
            CancellationToken cancellationToken = default)
        {
            var columnExists = await StoredFilesCompressionStateColumnExistsAsync(
                connection,
                transaction,
                cancellationToken);

            if (columnExists)
                return true;

            return await base.ApplyAsync(connection, transaction, logger, cancellationToken);
        }

        private static async Task<bool> StoredFilesCompressionStateColumnExistsAsync(
            IDbConnection connection,
            IDbTransaction? transaction,
            CancellationToken cancellationToken)
        {
            var columns = await connection.QueryAsync<string>(new CommandDefinition(
                "SELECT name FROM pragma_table_info('stored_files');",
                transaction: transaction,
                cancellationToken: cancellationToken));

            return columns.Contains("filestore_state_compress", StringComparer.OrdinalIgnoreCase);
        }
    }
}
