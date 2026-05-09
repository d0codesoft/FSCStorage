namespace SCP.StorageFSC.Data.Schema
{
    public sealed class DbSchemaV5 : DbSchemaBase
    {
        public override int CurrentSchemaVersion => 5;

        public override string Name => "Track deleted tenants for deferred file cleanup";

        protected override string Sql => """
            CREATE TABLE IF NOT EXISTS deleted_tenants
            (
                id                   BLOB NOT NULL PRIMARY KEY CHECK(length(id) = 16),
                tenant_id            BLOB NOT NULL CHECK(length(tenant_id) = 16),
                user_id              BLOB NOT NULL CHECK(length(user_id) = 16),
                tenant_guid          BLOB NOT NULL CHECK(length(tenant_guid) = 16),
                tenant_name          TEXT NOT NULL,
                deleted_utc          TEXT NOT NULL,
                cleanup_completed_utc TEXT NULL,
                created_utc          TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
                updated_utc          TEXT NULL,
                row_version          BLOB NOT NULL CHECK(length(row_version) = 16)
            );

            CREATE INDEX IF NOT EXISTS ix_deleted_tenants_cleanup_completed_utc
                ON deleted_tenants(cleanup_completed_utc);

            CREATE INDEX IF NOT EXISTS ix_deleted_tenants_deleted_utc
                ON deleted_tenants(deleted_utc);
            """;
    }
}
