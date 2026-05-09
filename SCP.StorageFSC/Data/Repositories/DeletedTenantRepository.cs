using Dapper;
using Microsoft.Data.Sqlite;
using SCP.StorageFSC.Data.Models;
using System.Data;

namespace SCP.StorageFSC.Data.Repositories
{
    public sealed class DeletedTenantRepository : IDeletedTenantRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public DeletedTenantRepository(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public async Task<bool> InsertAsync(DeletedTenant deletedTenant, CancellationToken cancellationToken = default)
        {
            const string sql = """
                INSERT INTO deleted_tenants
                (
                    id,
                    tenant_id,
                    user_id,
                    tenant_guid,
                    tenant_name,
                    deleted_utc,
                    cleanup_completed_utc,
                    created_utc,
                    updated_utc,
                    row_version
                )
                VALUES
                (
                    @Id,
                    @TenantId,
                    @UserId,
                    @TenantGuid,
                    @TenantName,
                    @DeletedUtc,
                    @CleanupCompletedUtc,
                    @CreatedUtc,
                    @UpdatedUtc,
                    @RowVersion
                );
                """;

            try
            {
                using var connection = _connectionFactory.CreateConnection();
                var affected = await connection.ExecuteAsync(new CommandDefinition(
                    sql,
                    deletedTenant,
                    cancellationToken: cancellationToken));

                return affected > 0;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (DataException ex)
            {
                throw new RepositoryException($"Failed to insert deleted tenant '{deletedTenant.TenantId}' due to data mapping error.", ex);
            }
            catch (SqliteException ex)
            {
                throw new RepositoryException($"Failed to insert deleted tenant '{deletedTenant.TenantId}' due to database error.", ex);
            }
        }

        public async Task<IReadOnlyList<DeletedTenant>> GetPendingCleanupAsync(CancellationToken cancellationToken = default)
        {
            const string sql = """
                SELECT
                    id AS Id,
                    tenant_id AS TenantId,
                    user_id AS UserId,
                    tenant_guid AS TenantGuid,
                    tenant_name AS TenantName,
                    deleted_utc AS DeletedUtc,
                    cleanup_completed_utc AS CleanupCompletedUtc,
                    created_utc AS CreatedUtc,
                    updated_utc AS UpdatedUtc,
                    row_version AS RowVersion
                FROM deleted_tenants
                WHERE cleanup_completed_utc IS NULL
                ORDER BY deleted_utc, id;
                """;

            try
            {
                using var connection = _connectionFactory.CreateConnection();
                var rows = await connection.QueryAsync<DeletedTenant>(new CommandDefinition(
                    sql,
                    cancellationToken: cancellationToken));

                return rows.ToList();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (DataException ex)
            {
                throw new RepositoryException("Failed to load pending deleted tenants due to data mapping error.", ex);
            }
            catch (SqliteException ex)
            {
                throw new RepositoryException("Failed to load pending deleted tenants due to database error.", ex);
            }
        }

        public async Task<int> MarkCleanupCompletedAsync(
            IReadOnlyCollection<Guid> ids,
            DateTime completedUtc,
            CancellationToken cancellationToken = default)
        {
            if (ids.Count == 0)
                return 0;

            const string sql = """
                UPDATE deleted_tenants
                SET
                    cleanup_completed_utc = @CompletedUtc,
                    updated_utc = @CompletedUtc,
                    row_version = @RowVersion
                WHERE id IN @Ids
                  AND cleanup_completed_utc IS NULL;
                """;

            try
            {
                using var connection = _connectionFactory.CreateConnection();
                return await connection.ExecuteAsync(new CommandDefinition(
                    sql,
                    new
                    {
                        Ids = ids,
                        CompletedUtc = completedUtc,
                        RowVersion = Guid.NewGuid()
                    },
                    cancellationToken: cancellationToken));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (DataException ex)
            {
                throw new RepositoryException("Failed to mark deleted tenant cleanup as completed due to data mapping error.", ex);
            }
            catch (SqliteException ex)
            {
                throw new RepositoryException("Failed to mark deleted tenant cleanup as completed due to database error.", ex);
            }
        }
    }
}
