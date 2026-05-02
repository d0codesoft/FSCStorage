using Dapper;
using Microsoft.Data.Sqlite;
using SCP.StorageFSC.Data.Models;
using System.Data;

namespace SCP.StorageFSC.Data.Repositories
{
    public sealed class BackgroundTaskRepository : IBackgroundTaskRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public BackgroundTaskRepository(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public async Task<Guid> InsertIfNotExistsAsync(
            BackgroundTask task,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(task);

            const string sql = """
                INSERT OR IGNORE INTO background_tasks
                (
                    id,
                    task_id,
                    type,
                    status,
                    upload_id,
                    queued_at_utc,
                    started_at_utc,
                    completed_at_utc,
                    failed_at_utc,
                    error_message,
                    result_summary,
                    created_utc,
                    updated_utc,
                    row_version
                )
                VALUES
                (
                    @Id,
                    @TaskId,
                    @Type,
                    @Status,
                    @UploadId,
                    @QueuedAtUtc,
                    @StartedAtUtc,
                    @CompletedAtUtc,
                    @FailedAtUtc,
                    @ErrorMessage,
                    @ResultSummary,
                    @CreatedUtc,
                    @UpdatedUtc,
                    @RowVersion
                );
                """;

            try
            {
                using var connection = _connectionFactory.CreateConnection();

                await connection.ExecuteAsync(new CommandDefinition(
                    sql,
                    ToParameters(task),
                    cancellationToken: cancellationToken));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (DataException ex)
            {
                throw new RepositoryException($"Failed to insert background task '{task.TaskId}' due to data mapping error.", ex);
            }
            catch (SqliteException ex)
            {
                throw new RepositoryException($"Failed to insert background task '{task.TaskId}' due to database error.", ex);
            }

            return task.Id;
        }

        public async Task<BackgroundTask?> GetByTaskIdAsync(
            Guid taskId,
            CancellationToken cancellationToken = default)
        {
            const string sql = SelectBaseSql + """

                WHERE task_id = @TaskId
                LIMIT 1;
                """;

            try
            {
                using var connection = _connectionFactory.CreateConnection();

                return await connection.QuerySingleOrDefaultAsync<BackgroundTask>(new CommandDefinition(
                    sql,
                    new { TaskId = taskId },
                    cancellationToken: cancellationToken));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (DataException ex)
            {
                throw new RepositoryException($"Failed to load background task '{taskId}' due to data mapping error.", ex);
            }
            catch (SqliteException ex)
            {
                throw new RepositoryException($"Failed to load background task '{taskId}' due to database error.", ex);
            }
        }

        public async Task<IReadOnlyList<BackgroundTask>> GetActiveAsync(
            CancellationToken cancellationToken = default)
        {
            const string sql = SelectBaseSql + """

                WHERE status IN (0, 1)
                ORDER BY queued_at_utc ASC;
                """;

            try
            {
                using var connection = _connectionFactory.CreateConnection();

                var result = await connection.QueryAsync<BackgroundTask>(new CommandDefinition(
                    sql,
                    cancellationToken: cancellationToken));

                return result.AsList();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (DataException ex)
            {
                throw new RepositoryException("Failed to load active background tasks due to data mapping error.", ex);
            }
            catch (SqliteException ex)
            {
                throw new RepositoryException("Failed to load active background tasks due to database error.", ex);
            }
        }

        public async Task<IReadOnlyList<BackgroundTask>> GetCompletedAsync(
            int limit = 100,
            CancellationToken cancellationToken = default)
        {
            const string sql = SelectBaseSql + """

                WHERE status IN (2, 3, 4)
                ORDER BY COALESCE(completed_at_utc, failed_at_utc, updated_utc, queued_at_utc) DESC
                LIMIT @Limit;
                """;

            try
            {
                using var connection = _connectionFactory.CreateConnection();

                var result = await connection.QueryAsync<BackgroundTask>(new CommandDefinition(
                    sql,
                    new { Limit = Math.Clamp(limit, 1, 500) },
                    cancellationToken: cancellationToken));

                return result.AsList();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (DataException ex)
            {
                throw new RepositoryException("Failed to load completed background tasks due to data mapping error.", ex);
            }
            catch (SqliteException ex)
            {
                throw new RepositoryException("Failed to load completed background tasks due to database error.", ex);
            }
        }

        public Task<bool> MarkRunningAsync(
            Guid taskId,
            DateTime startedAtUtc,
            CancellationToken cancellationToken = default)
        {
            const string sql = """
                UPDATE background_tasks
                SET
                    status = @Status,
                    started_at_utc = COALESCE(started_at_utc, @StartedAtUtc),
                    updated_utc = @StartedAtUtc,
                    row_version = @RowVersion
                WHERE task_id = @TaskId;
                """;

            return ExecuteStatusUpdateAsync(
                sql,
                new
                {
                    TaskId = taskId,
                    Status = (short)BackgroundTaskStatus.Running,
                    StartedAtUtc = startedAtUtc,
                    RowVersion = Guid.NewGuid()
                },
                $"mark background task '{taskId}' as running",
                cancellationToken);
        }

        public Task<bool> MarkCompletedAsync(
            Guid taskId,
            DateTime completedAtUtc,
            string? resultSummary = null,
            CancellationToken cancellationToken = default)
        {
            const string sql = """
                UPDATE background_tasks
                SET
                    status = @Status,
                    completed_at_utc = @CompletedAtUtc,
                    error_message = NULL,
                    result_summary = @ResultSummary,
                    updated_utc = @CompletedAtUtc,
                    row_version = @RowVersion
                WHERE task_id = @TaskId;
                """;

            return ExecuteStatusUpdateAsync(
                sql,
                new
                {
                    TaskId = taskId,
                    Status = (short)BackgroundTaskStatus.Completed,
                    CompletedAtUtc = completedAtUtc,
                    ResultSummary = resultSummary,
                    RowVersion = Guid.NewGuid()
                },
                $"mark background task '{taskId}' as completed",
                cancellationToken);
        }

        public Task<bool> MarkFailedAsync(
            Guid taskId,
            DateTime failedAtUtc,
            string errorMessage,
            CancellationToken cancellationToken = default)
        {
            const string sql = """
                UPDATE background_tasks
                SET
                    status = @Status,
                    failed_at_utc = @FailedAtUtc,
                    error_message = @ErrorMessage,
                    updated_utc = @FailedAtUtc,
                    row_version = @RowVersion
                WHERE task_id = @TaskId;
                """;

            return ExecuteStatusUpdateAsync(
                sql,
                new
                {
                    TaskId = taskId,
                    Status = (short)BackgroundTaskStatus.Failed,
                    FailedAtUtc = failedAtUtc,
                    ErrorMessage = errorMessage,
                    RowVersion = Guid.NewGuid()
                },
                $"mark background task '{taskId}' as failed",
                cancellationToken);
        }

        public Task<bool> MarkCanceledAsync(
            Guid taskId,
            DateTime canceledAtUtc,
            string? resultSummary = null,
            CancellationToken cancellationToken = default)
        {
            const string sql = """
                UPDATE background_tasks
                SET
                    status = @Status,
                    failed_at_utc = @CanceledAtUtc,
                    error_message = NULL,
                    result_summary = @ResultSummary,
                    updated_utc = @CanceledAtUtc,
                    row_version = @RowVersion
                WHERE task_id = @TaskId;
                """;

            return ExecuteStatusUpdateAsync(
                sql,
                new
                {
                    TaskId = taskId,
                    Status = (short)BackgroundTaskStatus.Canceled,
                    CanceledAtUtc = canceledAtUtc,
                    ResultSummary = resultSummary,
                    RowVersion = Guid.NewGuid()
                },
                $"mark background task '{taskId}' as canceled",
                cancellationToken);
        }

        public async Task<int> DeleteCompletedOlderThanAsync(
            DateTime cutoffUtc,
            CancellationToken cancellationToken = default)
        {
            const string sql = """
                DELETE FROM background_tasks
                WHERE status IN (2, 3, 4)
                  AND COALESCE(completed_at_utc, failed_at_utc, updated_utc, queued_at_utc) < @CutoffUtc;
                """;

            try
            {
                using var connection = _connectionFactory.CreateConnection();

                return await connection.ExecuteAsync(new CommandDefinition(
                    sql,
                    new { CutoffUtc = cutoffUtc },
                    cancellationToken: cancellationToken));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (DataException ex)
            {
                throw new RepositoryException("Failed to delete old completed background tasks due to data mapping error.", ex);
            }
            catch (SqliteException ex)
            {
                throw new RepositoryException("Failed to delete old completed background tasks due to database error.", ex);
            }
        }

        private async Task<bool> ExecuteStatusUpdateAsync(
            string sql,
            object parameters,
            string operation,
            CancellationToken cancellationToken)
        {
            try
            {
                using var connection = _connectionFactory.CreateConnection();

                var affected = await connection.ExecuteAsync(new CommandDefinition(
                    sql,
                    parameters,
                    cancellationToken: cancellationToken));

                return affected > 0;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (DataException ex)
            {
                throw new RepositoryException($"Failed to {operation} due to data mapping error.", ex);
            }
            catch (SqliteException ex)
            {
                throw new RepositoryException($"Failed to {operation} due to database error.", ex);
            }
        }

        private static object ToParameters(BackgroundTask task)
        {
            return new
            {
                task.Id,
                task.TaskId,
                task.Type,
                Status = (short)task.Status,
                task.UploadId,
                task.QueuedAtUtc,
                task.StartedAtUtc,
                task.CompletedAtUtc,
                task.FailedAtUtc,
                task.ErrorMessage,
                task.ResultSummary,
                task.CreatedUtc,
                task.UpdatedUtc,
                task.RowVersion
            };
        }

        private const string SelectBaseSql = """
            SELECT
                id AS Id,
                task_id AS TaskId,
                type AS Type,
                status AS Status,
                upload_id AS UploadId,
                queued_at_utc AS QueuedAtUtc,
                started_at_utc AS StartedAtUtc,
                completed_at_utc AS CompletedAtUtc,
                failed_at_utc AS FailedAtUtc,
                error_message AS ErrorMessage,
                result_summary AS ResultSummary,
                created_utc AS CreatedUtc,
                updated_utc AS UpdatedUtc,
                row_version AS RowVersion
            FROM background_tasks
            """;
    }
}
