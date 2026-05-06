namespace SCP.StorageFSC.Data.Schema
{
    using Dapper;
    using SCP.StorageFSC.Data;
    using System.Data;

    public sealed class DbSchemaV4 : DbSchemaBase
    {
        public override int CurrentSchemaVersion => 4;

        public override string Name => "Add users table";

        protected override string Sql => """
        CREATE TABLE IF NOT EXISTS roles
        (
            id              BLOB    NOT NULL PRIMARY KEY CHECK(length(id) = 16),

            name            TEXT    NOT NULL,
            normalized_name TEXT    NOT NULL,
            description     TEXT    NULL,

            is_system       INTEGER NOT NULL DEFAULT 0 CHECK(is_system IN (0, 1)),

            created_utc     TEXT    NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
            updated_utc     TEXT    NULL,
            row_version     BLOB    NOT NULL DEFAULT (randomblob(16)) CHECK(length(row_version) = 16),

            CONSTRAINT uq_roles_normalized_name UNIQUE (normalized_name)
        );

        CREATE TABLE IF NOT EXISTS user_roles
        (
            id              BLOB    NOT NULL PRIMARY KEY CHECK(length(id) = 16),

            user_id         BLOB    NOT NULL CHECK(length(user_id) = 16),
            role_id         BLOB    NOT NULL CHECK(length(role_id) = 16),

            created_utc     TEXT    NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
            updated_utc     TEXT    NULL,
            row_version     BLOB    NOT NULL DEFAULT (randomblob(16)) CHECK(length(row_version) = 16),

            CONSTRAINT uq_user_roles_user_role UNIQUE (user_id, role_id),

            FOREIGN KEY(user_id) REFERENCES users(id) ON DELETE CASCADE,
            FOREIGN KEY(role_id) REFERENCES roles(id) ON DELETE CASCADE
        );

        CREATE TABLE IF NOT EXISTS users
        (
            id                                  BLOB    NOT NULL PRIMARY KEY CHECK(length(id) = 16),

            name                                TEXT    NOT NULL,
            normalized_name                     TEXT    NOT NULL,

            email                               TEXT    NULL,
            normalized_email                    TEXT    NULL,
            email_confirmed                     INTEGER NOT NULL DEFAULT 0 CHECK(email_confirmed IN (0, 1)),

            phone_number                        TEXT    NULL,
            phone_number_confirmed              INTEGER NOT NULL DEFAULT 0 CHECK(phone_number_confirmed IN (0, 1)),

            password_hash                       TEXT    NOT NULL,
            password_changed_utc                TEXT    NULL,

            security_stamp                      TEXT    NOT NULL,

            is_active                           INTEGER NOT NULL DEFAULT 1 CHECK(is_active IN (0, 1)),
            is_locked                           INTEGER NOT NULL DEFAULT 0 CHECK(is_locked IN (0, 1)),
            locked_until_utc                    TEXT    NULL,

            failed_login_count                  INTEGER NOT NULL DEFAULT 0,
            last_failed_login_utc               TEXT    NULL,
            last_login_utc                      TEXT    NULL,
            last_login_ip_address               TEXT    NULL,

            two_factor_enabled                  INTEGER NOT NULL DEFAULT 0 CHECK(two_factor_enabled IN (0, 1)),
            two_factor_required_for_every_login INTEGER NOT NULL DEFAULT 1 CHECK(two_factor_required_for_every_login IN (0, 1)),
            preferred_two_factor_method         INTEGER NOT NULL DEFAULT 1,
            two_factor_enabled_utc              TEXT    NULL,
            two_factor_last_used_utc            TEXT    NULL,

            must_change_password                INTEGER NOT NULL DEFAULT 0 CHECK(must_change_password IN (0, 1)),
            password_expires_utc                TEXT    NULL,

            external_user_id                    TEXT    NULL,
            comment                             TEXT    NULL,

            created_utc                         TEXT    NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
            updated_utc                         TEXT    NULL,
            row_version                         BLOB    NOT NULL CHECK(length(row_version) = 16),

            CONSTRAINT uq_users_normalized_name UNIQUE (normalized_name),
            CONSTRAINT uq_users_normalized_email UNIQUE (normalized_email)
        );

        CREATE TABLE IF NOT EXISTS user_two_factor_methods
        (
            id                          BLOB    NOT NULL PRIMARY KEY CHECK(length(id) = 16),
            user_id                     BLOB    NOT NULL CHECK(length(user_id) = 16),

            method_type                 INTEGER NOT NULL,
            is_enabled                  INTEGER NOT NULL DEFAULT 0 CHECK(is_enabled IN (0, 1)),
            is_confirmed                INTEGER NOT NULL DEFAULT 0 CHECK(is_confirmed IN (0, 1)),
            is_default                  INTEGER NOT NULL DEFAULT 0 CHECK(is_default IN (0, 1)),

            secret_encrypted            TEXT    NULL,
            destination                 TEXT    NULL,
            masked_destination          TEXT    NULL,

            confirmed_utc               TEXT    NULL,
            last_used_utc               TEXT    NULL,

            failed_attempt_count        INTEGER NOT NULL DEFAULT 0,
            last_failed_attempt_utc     TEXT    NULL,
            locked_until_utc            TEXT    NULL,

            created_utc                 TEXT    NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
            updated_utc                 TEXT    NULL,
            row_version                 BLOB    NOT NULL CHECK(length(row_version) = 16),

            FOREIGN KEY(user_id) REFERENCES users(id) ON DELETE CASCADE,

            CONSTRAINT uq_user_two_factor_methods_user_type_destination
                UNIQUE (user_id, method_type, destination)
        );

        CREATE INDEX IF NOT EXISTS ix_user_two_factor_methods_user_id
        ON user_two_factor_methods(user_id);

        CREATE INDEX IF NOT EXISTS ix_user_two_factor_methods_user_default
        ON user_two_factor_methods(user_id, is_default);

        CREATE TABLE IF NOT EXISTS user_two_factor_challenges
        (
            id                          BLOB    NOT NULL PRIMARY KEY CHECK(length(id) = 16),
            user_id                     BLOB    NOT NULL CHECK(length(user_id) = 16),
            user_two_factor_method_id   BLOB    NULL CHECK(user_two_factor_method_id IS NULL OR length(user_two_factor_method_id) = 16),

            method_type                 INTEGER NOT NULL,
            code_hash                   TEXT    NOT NULL,
            destination                 TEXT    NOT NULL,

            status                      INTEGER NOT NULL DEFAULT 0,
            expires_utc                 TEXT    NOT NULL,
            verified_utc                TEXT    NULL,

            failed_attempt_count        INTEGER NOT NULL DEFAULT 0,
            max_failed_attempt_count    INTEGER NOT NULL DEFAULT 5,

            created_ip_address          TEXT    NULL,
            verified_ip_address         TEXT    NULL,
            user_agent                  TEXT    NULL,

            created_utc                 TEXT    NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
            updated_utc                 TEXT    NULL,
            row_version                 BLOB    NOT NULL CHECK(length(row_version) = 16),

            FOREIGN KEY(user_id) REFERENCES users(id) ON DELETE CASCADE,
            FOREIGN KEY(user_two_factor_method_id) REFERENCES user_two_factor_methods(id) ON DELETE SET NULL
        );

        CREATE INDEX IF NOT EXISTS ix_user_two_factor_challenges_user_status
        ON user_two_factor_challenges(user_id, status);

        CREATE INDEX IF NOT EXISTS ix_user_two_factor_challenges_expires_utc
        ON user_two_factor_challenges(expires_utc);

        CREATE TABLE IF NOT EXISTS user_recovery_codes
        (
            id                  BLOB    NOT NULL PRIMARY KEY CHECK(length(id) = 16),
            user_id             BLOB    NOT NULL CHECK(length(user_id) = 16),

            code_hash           TEXT    NOT NULL,
            is_used             INTEGER NOT NULL DEFAULT 0 CHECK(is_used IN (0, 1)),

            used_utc            TEXT    NULL,
            used_ip_address     TEXT    NULL,
            used_user_agent     TEXT    NULL,

            created_utc         TEXT    NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
            updated_utc         TEXT    NULL,
            row_version         BLOB    NOT NULL CHECK(length(row_version) = 16),

            FOREIGN KEY(user_id) REFERENCES users(id) ON DELETE CASCADE,

            CONSTRAINT uq_user_recovery_codes_user_code_hash UNIQUE (user_id, code_hash)
        );

        CREATE INDEX IF NOT EXISTS ix_user_recovery_codes_user_unused
        ON user_recovery_codes(user_id, is_used);

        CREATE TABLE IF NOT EXISTS user_login_challenges
        (
            id                          BLOB    NOT NULL PRIMARY KEY CHECK(length(id) = 16),
            user_id                     BLOB    NOT NULL CHECK(length(user_id) = 16),

            challenge_token_hash        TEXT    NOT NULL,
            method_type                 INTEGER NOT NULL,
            two_factor_challenge_id     BLOB    NULL CHECK(two_factor_challenge_id IS NULL OR length(two_factor_challenge_id) = 16),

            status                      INTEGER NOT NULL DEFAULT 0,
            expires_utc                 TEXT    NOT NULL,
            completed_utc               TEXT    NULL,

            failed_attempt_count        INTEGER NOT NULL DEFAULT 0,
            max_failed_attempt_count    INTEGER NOT NULL DEFAULT 5,

            ip_address                  TEXT    NULL,
            user_agent                  TEXT    NULL,

            created_utc                 TEXT    NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
            updated_utc                 TEXT    NULL,
            row_version                 BLOB    NOT NULL CHECK(length(row_version) = 16),

            FOREIGN KEY(user_id) REFERENCES users(id) ON DELETE CASCADE,
            FOREIGN KEY(two_factor_challenge_id) REFERENCES user_two_factor_challenges(id) ON DELETE SET NULL,

            CONSTRAINT uq_user_login_challenges_token_hash UNIQUE (challenge_token_hash)
        );

        CREATE INDEX IF NOT EXISTS ix_user_login_challenges_user_status
        ON user_login_challenges(user_id, status);

        CREATE INDEX IF NOT EXISTS ix_user_login_challenges_expires_utc
        ON user_login_challenges(expires_utc);

        CREATE INDEX IF NOT EXISTS ix_user_roles_user_id
        ON user_roles(user_id);

        CREATE INDEX IF NOT EXISTS ix_user_roles_role_id
        ON user_roles(role_id);
        """;

        public override async Task<bool> ApplyAsync(
            IDbConnection connection,
            IDbTransaction? transaction,
            ILogger? logger,
            CancellationToken cancellationToken = default)
        {
            var tablesExist = await UsersTablesExistAsync(
                connection,
                transaction,
                cancellationToken);

            if (tablesExist)
                return true;

            return await base.ApplyAsync(connection, transaction, logger, cancellationToken);
        }

        private static async Task<bool> UsersTablesExistAsync(
            IDbConnection connection,
            IDbTransaction? transaction,
            CancellationToken cancellationToken)
        {
            const string sql = """
                SELECT name
                FROM sqlite_master
                WHERE type = 'table'
                  AND name IN (
                      'users',
                      'user_two_factor_methods',
                      'user_two_factor_challenges',
                      'user_recovery_codes',
                      'user_login_challenges');
                """;

            var tableNames = await connection.QueryAsync<string>(new CommandDefinition(
                sql,
                transaction: transaction,
                cancellationToken: cancellationToken));

            string[] requiredTables = [
                "users",
                "user_two_factor_methods",
                "user_two_factor_challenges",
                "user_recovery_codes",
                "user_login_challenges"
            ];

            return requiredTables.All(requiredTable =>
                tableNames.Contains(requiredTable, StringComparer.OrdinalIgnoreCase));
        }
    }
}
