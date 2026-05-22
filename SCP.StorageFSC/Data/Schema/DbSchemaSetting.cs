using Dapper;
using System.Data;

namespace scp.filestorage.Data.Schema
{
    public class DbSchemaSetting
    {
        public string Name => "Initial system settings on Database";

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
                    logger.LogError(ex, "Failed to apply default system settings to database");
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
            INSERT OR IGNORE INTO system_settings
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
                X'0196A5C21A0070008000000000000101',
                'FileTransferLimiter.MaxConcurrentUploads',
                '4',
                'Maximum number of concurrent uploads.',
                strftime('%Y-%m-%dT%H:%M:%fZ','now'),
                NULL,
                randomblob(16)
            );

            INSERT OR IGNORE INTO system_settings
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
                X'0196A5C21A0070008000000000000102',
                'FileTransferLimiter.MaxConcurrentDownloads',
                '20',
                'Maximum number of concurrent downloads.',
                strftime('%Y-%m-%dT%H:%M:%fZ','now'),
                NULL,
                randomblob(16)
            );

            INSERT OR IGNORE INTO system_settings
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
                X'0196A5C21A0070008000000000000103',
                'FileTransferLimiter.UploadBytesPerSecond',
                '52428800',
                'Total upload speed limit in bytes per second.',
                strftime('%Y-%m-%dT%H:%M:%fZ','now'),
                NULL,
                randomblob(16)
            );

            INSERT OR IGNORE INTO system_settings
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
                X'0196A5C21A0070008000000000000104',
                'FileTransferLimiter.DownloadBytesPerSecond',
                '104857600',
                'Total download speed limit in bytes per second.',
                strftime('%Y-%m-%dT%H:%M:%fZ','now'),
                NULL,
                randomblob(16)
            );

            INSERT OR IGNORE INTO system_settings
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
                X'0196A5C21A0070008000000000000201',
                'MultipartSetting.MinPartSizeBytes',
                '5242880',
                'Minimum multipart upload part size in bytes.',
                strftime('%Y-%m-%dT%H:%M:%fZ','now'),
                NULL,
                randomblob(16)
            );

            INSERT OR IGNORE INTO system_settings
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
                X'0196A5C21A0070008000000000000202',
                'MultipartSetting.MaxPartSizeBytes',
                '104857600',
                'Maximum multipart upload part size in bytes.',
                strftime('%Y-%m-%dT%H:%M:%fZ','now'),
                NULL,
                randomblob(16)
            );

            INSERT OR IGNORE INTO system_settings
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
                X'0196A5C21A0070008000000000000301',
                'FileStorageCleanup.Enabled',
                'true',
                'Enables background cleanup of completed tasks and terminal multipart sessions.',
                strftime('%Y-%m-%dT%H:%M:%fZ','now'),
                NULL,
                randomblob(16)
            );

            INSERT OR IGNORE INTO system_settings
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
                X'0196A5C21A0070008000000000000302',
                'FileStorageCleanup.CompletedTaskRetentionDays',
                '30',
                'Retention period for completed background tasks in days.',
                strftime('%Y-%m-%dT%H:%M:%fZ','now'),
                NULL,
                randomblob(16)
            );

            INSERT OR IGNORE INTO system_settings
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
                X'0196A5C21A0070008000000000000303',
                'FileStorageCleanup.MultipartUploadSessionRetentionDays',
                '30',
                'Retention period for terminal multipart upload sessions in days.',
                strftime('%Y-%m-%dT%H:%M:%fZ','now'),
                NULL,
                randomblob(16)
            );

            INSERT OR IGNORE INTO system_settings
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
                X'0196A5C21A0070008000000000000304',
                'FileStorageCleanup.InitialDelay',
                '00:05:00',
                'Initial delay before the cleanup worker starts.',
                strftime('%Y-%m-%dT%H:%M:%fZ','now'),
                NULL,
                randomblob(16)
            );

            INSERT OR IGNORE INTO system_settings
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
                X'0196A5C21A0070008000000000000305',
                'FileStorageCleanup.Interval',
                '1.00:00:00',
                'Interval between cleanup worker runs.',
                strftime('%Y-%m-%dT%H:%M:%fZ','now'),
                NULL,
                randomblob(16)
            );

            INSERT OR IGNORE INTO system_settings
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
                X'0196A5C21A0070008000000000000401',
                'Logging.LogLevel.Default',
                'Debug',
                'Default application log level.',
                strftime('%Y-%m-%dT%H:%M:%fZ','now'),
                NULL,
                randomblob(16)
            );
            """;
    }
}
