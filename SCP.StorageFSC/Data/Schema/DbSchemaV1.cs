namespace SCP.StorageFSC.Data.Schema
{
    public sealed class DbSchemaV1 : DbSchemaBase
    {
        public override int CurrentSchemaVersion => 1;

        public override string Name => "Initial schema";

        protected override string Sql => """
            CREATE TABLE IF NOT EXISTS db_metadata
            (
                id                 BLOB NOT NULL PRIMARY KEY CHECK(length(id) = 16),
                schema_version     INTEGER NOT NULL,
                schema_name        TEXT    NOT NULL,
                created_utc        TEXT    NOT NULL 
                                   DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
                updated_utc        TEXT    NULL,
                row_version        BLOB NOT NULL CHECK(length(row_version) = 16)
            );

            CREATE TABLE IF NOT EXISTS tenants
            (
                id                 BLOB NOT NULL PRIMARY KEY CHECK(length(id) = 16),
                external_tenant_id BLOB NOT NULL CHECK(length(external_tenant_id) = 16),
                name               TEXT    NOT NULL,
                is_active          INTEGER NOT NULL DEFAULT 1,
                created_utc        TEXT    NOT NULL 
                                   DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
                updated_utc        TEXT    NULL,
                row_version        BLOB NOT NULL CHECK(length(row_version) = 16),

                CONSTRAINT uq_tenants_external_tenant_id UNIQUE (external_tenant_id)
            );

            CREATE TABLE IF NOT EXISTS api_tokens
            (
                id            BLOB NOT NULL PRIMARY KEY CHECK(length(id) = 16),
                tenant_id     BLOB NULL CHECK(tenant_id IS NULL OR length(tenant_id)=16),
                name          TEXT    NOT NULL,
                token_hash    TEXT    NOT NULL,
                token_prefix  TEXT    NOT NULL,
                is_active     INTEGER NOT NULL DEFAULT 1,
                is_admin      INTEGER NOT NULL DEFAULT 0,
                can_read      INTEGER NOT NULL DEFAULT 1,
                can_write     INTEGER NOT NULL DEFAULT 0,
                can_delete    INTEGER NOT NULL DEFAULT 0,
                last_used_utc TEXT    NULL,
                expires_utc   TEXT    NULL,
                revoked_utc   TEXT    NULL,
                created_utc   TEXT    NOT NULL 
                                   DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
                updated_utc   TEXT    NULL,
                row_version   BLOB NOT NULL CHECK(length(row_version) = 16),
            
                CONSTRAINT uq_api_tokens_token_hash UNIQUE (token_hash),
                FOREIGN KEY (tenant_id) REFERENCES tenants(id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS api_token_connection_logs
            (
                id                 BLOB NOT NULL PRIMARY KEY CHECK(length(id) = 16),
                api_token_id       BLOB    NULL CHECK(api_token_id IS NULL OR length(api_token_id)=16),
                token_name         TEXT    NOT NULL,
                tenant_id          BLOB    NULL CHECK(tenant_id IS NULL OR length(tenant_id)=16),
                external_tenant_id BLOB NOT NULL CHECK(length(external_tenant_id) = 16),
                tenant_name        TEXT    NOT NULL,
                is_success         INTEGER NOT NULL DEFAULT 1,
                error_message      TEXT    NULL,
                is_admin           INTEGER NOT NULL DEFAULT 0,
                client_ip          TEXT    NOT NULL,
                ip_source          TEXT    NOT NULL,
                forwarded_for_raw  TEXT    NULL,
                real_ip_raw        TEXT    NULL,
                request_path       TEXT    NOT NULL,
                user_agent         TEXT    NULL,
                created_utc        TEXT    NOT NULL
                                   DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
                updated_utc        TEXT    NULL,
                row_version        BLOB NOT NULL CHECK(length(row_version) = 16),
            
                FOREIGN KEY (api_token_id) REFERENCES api_tokens(id) ON DELETE CASCADE,
                FOREIGN KEY (tenant_id) REFERENCES tenants(id) ON DELETE SET NULL
            );

            CREATE TABLE IF NOT EXISTS stored_files
            (
                id                 BLOB NOT NULL PRIMARY KEY CHECK(length(id) = 16),
                sha256             TEXT    NOT NULL,
                crc32              TEXT    NOT NULL,
                file_size          INTEGER NOT NULL,
                physical_path      TEXT    NOT NULL,
                original_file_name TEXT    NOT NULL,
                content_type       TEXT    NULL,
                filestore_state_compress INTEGER NOT NULL DEFAULT 0,
                reference_count    INTEGER NOT NULL DEFAULT 0,
                deleted_utc        TEXT    NULL,
                is_deleted         INTEGER NOT NULL DEFAULT 0,
                created_utc        TEXT    NOT NULL
                                   DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
                updated_utc        TEXT    NULL,
                row_version        BLOB NOT NULL CHECK(length(row_version) = 16),
                        
                CONSTRAINT uq_stored_files_sha256 UNIQUE (sha256),
                CONSTRAINT uq_stored_files_physical_path UNIQUE (physical_path)
            );

            CREATE TABLE IF NOT EXISTS tenant_files
            (
                id             BLOB NOT NULL PRIMARY KEY CHECK(length(id) = 16),
                tenant_id      BLOB NOT NULL CHECK(length(tenant_id) = 16),
                stored_file_id BLOB NOT NULL CHECK(length(stored_file_id) = 16),
                file_guid      BLOB NOT NULL CHECK(length(file_guid) = 16),
                file_name      TEXT    NOT NULL,
                category       TEXT    NULL,
                external_key   TEXT    NULL,
                is_active      INTEGER NOT NULL DEFAULT 1,
                deleted_utc    TEXT    NULL,
                created_utc    TEXT    NOT NULL
                               DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
                updated_utc    TEXT    NULL,
                row_version    BLOB NOT NULL CHECK(length(row_version) = 16),
                        
                CONSTRAINT uq_tenant_files_file_guid UNIQUE (file_guid),
                FOREIGN KEY (tenant_id) REFERENCES tenants(id) ON DELETE CASCADE,
                FOREIGN KEY (stored_file_id) REFERENCES stored_files(id) ON DELETE RESTRICT
            );

            CREATE TABLE IF NOT EXISTS multipart_upload_sessions
            (
                id                         BLOB NOT NULL PRIMARY KEY CHECK(length(id) = 16),
                upload_id                  BLOB NOT NULL CHECK(length(upload_id) = 16),
                tenant_id                  BLOB NOT NULL CHECK(length(tenant_id) = 16),
                original_file_name         TEXT    NOT NULL,
                normalized_file_name       TEXT    NOT NULL,
                extension                  TEXT    NOT NULL,
                content_type               TEXT    NULL,
                total_file_size            INTEGER NOT NULL,
                part_size                  INTEGER NOT NULL,
                total_parts                INTEGER NOT NULL,
                expected_checksum_sha256   TEXT    NULL,
                final_checksum_sha256      TEXT    NULL,
                status                     INTEGER NOT NULL DEFAULT 0,
                error_code                 TEXT    NULL,
                error_message              TEXT    NULL,
                failed_at_utc              TEXT    NULL,
                storage_provider           TEXT    NOT NULL,
                temp_storage_bucket        TEXT    NULL,
                temp_storage_prefix        TEXT    NOT NULL,
                completed_at_utc           TEXT    NULL,
                expires_at_utc             TEXT    NULL,
                stored_file_id             BLOB NULL CHECK(length(stored_file_id) = 16),
                created_utc                TEXT    NOT NULL
                                           DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
                updated_utc                TEXT    NULL,
                row_version                BLOB NOT NULL CHECK(length(row_version) = 16),
            
                CONSTRAINT uq_multipart_upload_sessions_upload_id UNIQUE (upload_id)
            );

            CREATE TABLE IF NOT EXISTS multipart_upload_parts
            (
                id                           BLOB NOT NULL PRIMARY KEY CHECK(length(id) = 16),
                multipart_upload_session_id  BLOB NOT NULL CHECK(length(multipart_upload_session_id) = 16),
                part_number                  INTEGER NOT NULL DEFAULT 0,

                offset_bytes                 INTEGER NOT NULL,
                size_in_bytes                INTEGER NOT NULL,

                storage_key                  TEXT    NOT NULL,
                checksum_sha256              TEXT    NULL,
                provider_part_etag           TEXT    NULL,

                status                       INTEGER NOT NULL DEFAULT 0,

                uploaded_at_utc              TEXT    NULL,
                error_message                TEXT    NULL,
                retry_count                  INTEGER NOT NULL DEFAULT 0,
                last_failed_at_utc           TEXT    NULL,

                created_utc                TEXT    NOT NULL
                                           DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
                updated_utc                TEXT    NULL,
                row_version                BLOB NOT NULL CHECK(length(row_version) = 16),
            
                CONSTRAINT uq_multipart_upload_parts_session_part UNIQUE (multipart_upload_session_id, part_number),

                FOREIGN KEY (multipart_upload_session_id)
                    REFERENCES multipart_upload_sessions(id)
                    ON DELETE CASCADE
            );

            CREATE INDEX IF NOT EXISTS ix_multipart_upload_sessions_tenant_id
                ON multipart_upload_sessions(tenant_id);

            CREATE INDEX IF NOT EXISTS ix_multipart_upload_sessions_status
                ON multipart_upload_sessions(status);

            CREATE INDEX IF NOT EXISTS ix_multipart_upload_sessions_expires_at_utc
                ON multipart_upload_sessions(expires_at_utc);

            CREATE INDEX IF NOT EXISTS ix_multipart_upload_parts_session_id
                ON multipart_upload_parts(multipart_upload_session_id);

            CREATE INDEX IF NOT EXISTS ix_multipart_upload_parts_status
                ON multipart_upload_parts(status);

            CREATE INDEX IF NOT EXISTS ix_api_tokens_tenant_id
                ON api_tokens(tenant_id);

            CREATE INDEX IF NOT EXISTS ix_api_tokens_token_prefix
                ON api_tokens(token_prefix);

            CREATE INDEX IF NOT EXISTS ix_api_tokens_active
                ON api_tokens(is_active);

            CREATE INDEX IF NOT EXISTS ix_api_token_connection_logs_api_token_id
                ON api_token_connection_logs(api_token_id);

            CREATE INDEX IF NOT EXISTS ix_api_token_connection_logs_tenant_id
                ON api_token_connection_logs(tenant_id);

            CREATE INDEX IF NOT EXISTS ix_api_token_connection_logs_created_utc
                ON api_token_connection_logs(created_utc);

            CREATE INDEX IF NOT EXISTS ix_api_token_connection_logs_client_ip
                ON api_token_connection_logs(client_ip);

            CREATE INDEX IF NOT EXISTS ix_stored_files_crc32
                ON stored_files(crc32);

            CREATE INDEX IF NOT EXISTS ix_stored_files_is_deleted
                ON stored_files(is_deleted);

            CREATE INDEX IF NOT EXISTS ix_stored_files_reference_count
                ON stored_files(reference_count);

            CREATE INDEX IF NOT EXISTS ix_tenant_files_tenant_id
                ON tenant_files(tenant_id);

            CREATE INDEX IF NOT EXISTS ix_tenant_files_stored_file_id
                ON tenant_files(stored_file_id);

            CREATE INDEX IF NOT EXISTS ix_tenant_files_tenant_active
                ON tenant_files(tenant_id, is_active);

            CREATE INDEX IF NOT EXISTS ix_tenant_files_external_key
                ON tenant_files(external_key);
            """;
    }
}
