namespace SCP.StorageFSC.Data.Schema
{
    public sealed class DbSchemaV6 : DbSchemaBase
    {
        public override int CurrentSchemaVersion => 6;

        public override string Name => "Add system settings";

        protected override string Sql => """
            CREATE TABLE IF NOT EXISTS system_settings
            (
                id            BLOB NOT NULL PRIMARY KEY CHECK(length(id) = 16),
                name          TEXT NOT NULL,
                value         TEXT NOT NULL,
                description   TEXT NULL,
                created_utc   TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
                updated_utc   TEXT NULL,
                row_version   BLOB NOT NULL CHECK(length(row_version) = 16),

                CONSTRAINT uq_system_settings_name UNIQUE (name)
            );

            CREATE INDEX IF NOT EXISTS ix_system_settings_name
                ON system_settings(name);
            """;
    }
}
