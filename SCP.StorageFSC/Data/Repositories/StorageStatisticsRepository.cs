using Dapper;
using Microsoft.Data.Sqlite;
using SCP.StorageFSC.Data.Dto;
using System.Data;

namespace SCP.StorageFSC.Data.Repositories
{
    public sealed class StorageStatisticsRepository : IStorageStatisticsRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public StorageStatisticsRepository(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public async Task<StorageStatisticsDto> GetAsync(
            int largestFilesLimit = 25,
            CancellationToken cancellationToken = default)
        {
            const string summarySql = """
                SELECT
                    COALESCE(SUM(file_size), 0) AS UsedBytes,
                    COUNT(1) AS StoredFileCount
                FROM stored_files
                WHERE is_deleted = 0;
                """;

            const string tenantFileCountSql = """
                SELECT COUNT(1)
                FROM tenant_files tf
                INNER JOIN stored_files sf ON sf.id = tf.stored_file_id
                WHERE tf.is_active = 1
                  AND sf.is_deleted = 0;
                """;

            const string tenantCountSql = """
                SELECT COUNT(1)
                FROM tenants
                WHERE is_active = 1;
                """;

            const string largestFilesSql = """
                SELECT
                    tf.file_guid AS FileGuid,
                    tf.file_name AS FileName,
                    tf.category AS Category,
                    tf.external_key AS ExternalKey,
                    t.id AS TenantId,
                    t.external_tenant_id AS TenantGuid,
                    t.name AS TenantName,
                    sf.file_size AS FileSize,
                    tf.created_utc AS CreatedUtc
                FROM tenant_files tf
                INNER JOIN stored_files sf ON sf.id = tf.stored_file_id
                INNER JOIN tenants t ON t.id = tf.tenant_id
                WHERE tf.is_active = 1
                  AND sf.is_deleted = 0
                ORDER BY sf.file_size DESC, tf.created_utc DESC
                LIMIT @Limit;
                """;

            const string tenantsSql = """
                SELECT
                    t.id AS TenantId,
                    t.external_tenant_id AS TenantGuid,
                    t.name AS TenantName,
                    t.is_active AS IsActive,
                    COUNT(sf.id) AS FileCount,
                    COALESCE(SUM(sf.file_size), 0) AS UsedBytes
                FROM tenants t
                LEFT JOIN tenant_files tf
                    ON tf.tenant_id = t.id
                   AND tf.is_active = 1
                LEFT JOIN stored_files sf
                    ON sf.id = tf.stored_file_id
                   AND sf.is_deleted = 0
                GROUP BY t.id, t.external_tenant_id, t.name, t.is_active
                ORDER BY UsedBytes DESC, FileCount DESC, t.name;
                """;

            try
            {
                using var connection = _connectionFactory.CreateConnection();
                var limit = Math.Clamp(largestFilesLimit, 1, 100);

                var summary = await connection.QuerySingleAsync<StorageStatisticsDto>(new CommandDefinition(
                    summarySql,
                    cancellationToken: cancellationToken));

                summary.TenantFileCount = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                    tenantFileCountSql,
                    cancellationToken: cancellationToken));

                summary.TenantCount = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                    tenantCountSql,
                    cancellationToken: cancellationToken));

                var largestFiles = await connection.QueryAsync<LargestFileDto>(new CommandDefinition(
                    largestFilesSql,
                    new { Limit = limit },
                    cancellationToken: cancellationToken));

                var tenants = await connection.QueryAsync<TenantStorageStatisticsDto>(new CommandDefinition(
                    tenantsSql,
                    cancellationToken: cancellationToken));

                summary.LargestFiles = largestFiles.AsList();
                summary.Tenants = tenants.AsList();

                return summary;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (DataException ex)
            {
                throw new RepositoryException("Failed to load storage statistics due to data mapping error.", ex);
            }
            catch (SqliteException ex)
            {
                throw new RepositoryException("Failed to load storage statistics due to database error.", ex);
            }
        }
    }
}
