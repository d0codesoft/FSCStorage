namespace SCP.StorageFSC.Data.Schema
{
    public sealed class DbSchemaV7 : DbSchemaBase
    {
        public override int CurrentSchemaVersion => 7;

        public override string Name => "Add user authentication audit logs";

        protected override string Sql => """
            CREATE TABLE IF NOT EXISTS user_authentication_audit_logs
            (
                id                 BLOB NOT NULL PRIMARY KEY CHECK(length(id) = 16),
                user_id            BLOB NULL CHECK(user_id IS NULL OR length(user_id) = 16),
                user_name          TEXT NULL,
                login              TEXT NULL,
                event_type         TEXT NOT NULL,
                status             TEXT NOT NULL,
                is_success         INTEGER NOT NULL DEFAULT 0,
                failure_reason     TEXT NULL,
                client_ip          TEXT NOT NULL,
                ip_source          TEXT NOT NULL,
                forwarded_for_raw  TEXT NULL,
                real_ip_raw        TEXT NULL,
                request_path       TEXT NOT NULL,
                user_agent         TEXT NULL,
                created_utc        TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
                updated_utc        TEXT NULL,
                row_version        BLOB NOT NULL CHECK(length(row_version) = 16),

                FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE SET NULL
            );

            CREATE INDEX IF NOT EXISTS ix_user_auth_audit_user_id
                ON user_authentication_audit_logs(user_id);

            CREATE INDEX IF NOT EXISTS ix_user_auth_audit_created_utc
                ON user_authentication_audit_logs(created_utc);

            CREATE INDEX IF NOT EXISTS ix_user_auth_audit_client_ip
                ON user_authentication_audit_logs(client_ip);

            CREATE INDEX IF NOT EXISTS ix_user_auth_audit_is_success
                ON user_authentication_audit_logs(is_success);

            CREATE INDEX IF NOT EXISTS ix_user_auth_audit_event_type
                ON user_authentication_audit_logs(event_type);
            """;
    }
}
