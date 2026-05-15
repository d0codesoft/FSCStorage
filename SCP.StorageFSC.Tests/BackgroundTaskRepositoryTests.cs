using Dapper;
using Microsoft.Data.Sqlite;
using scp.filestorage.Data.Handlers;
using SCP.StorageFSC.Data;
using SCP.StorageFSC.Data.Models;
using SCP.StorageFSC.Data.Repositories;
using System.Data;

namespace SCP.StorageFSC.Tests;

public sealed class BackgroundTaskRepositoryTests
{
    [Fact]
    public async Task GetActiveAndCompletedAsync_QueryBackgroundTasks()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        DapperTypeHandlers.Register();

        var databasePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.db");
        var connectionString = $"Data Source={databasePath};Pooling=False";

        try
        {
            await using (var connection = new SqliteConnection(connectionString))
            {
                await connection.OpenAsync(cancellationToken);
                await CreateSchemaAsync(connection);

                await InsertTaskAsync(connection, BackgroundTaskStatus.Queued);
                await InsertTaskAsync(connection, BackgroundTaskStatus.Running);
                await InsertTaskAsync(connection, BackgroundTaskStatus.Completed);
                await InsertTaskAsync(connection, BackgroundTaskStatus.Failed);
                await InsertTaskAsync(connection, BackgroundTaskStatus.Canceled);
            }

            var repository = new BackgroundTaskRepository(new TestConnectionFactory(connectionString));

            var active = await repository.GetActiveAsync(cancellationToken);
            var completed = await repository.GetCompletedAsync(cancellationToken: cancellationToken);

            Assert.Equal(2, active.Count);
            Assert.All(active, task => Assert.True(
                task.Status is BackgroundTaskStatus.Queued or BackgroundTaskStatus.Running));
            Assert.Equal(3, completed.Count);
            Assert.All(completed, task => Assert.True(
                task.Status is BackgroundTaskStatus.Completed or BackgroundTaskStatus.Failed or BackgroundTaskStatus.Canceled));
        }
        finally
        {
            SqliteConnection.ClearAllPools();

            if (File.Exists(databasePath))
                File.Delete(databasePath);
        }
    }

    [Fact]
    public async Task MarkCanceledAsync_StoresCanceledTerminalState()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        DapperTypeHandlers.Register();

        var databasePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.db");
        var connectionString = $"Data Source={databasePath};Pooling=False";
        var taskId = Guid.NewGuid();
        var canceledAtUtc = DateTime.UtcNow;

        try
        {
            await using (var connection = new SqliteConnection(connectionString))
            {
                await connection.OpenAsync(cancellationToken);
                await CreateSchemaAsync(connection);
                await InsertTaskAsync(connection, BackgroundTaskStatus.Running, taskId);
            }

            var repository = new BackgroundTaskRepository(new TestConnectionFactory(connectionString));

            var updated = await repository.MarkCanceledAsync(
                taskId,
                canceledAtUtc,
                "Application shutdown canceled the task.",
                cancellationToken);
            var task = await repository.GetByTaskIdAsync(taskId, cancellationToken);

            Assert.True(updated);
            Assert.NotNull(task);
            Assert.Equal(BackgroundTaskStatus.Canceled, task.Status);
            Assert.Null(task.ErrorMessage);
            Assert.Equal("Application shutdown canceled the task.", task.ResultSummary);
            Assert.NotNull(task.FailedAtUtc);
        }
        finally
        {
            SqliteConnection.ClearAllPools();

            if (File.Exists(databasePath))
                File.Delete(databasePath);
        }
    }

    private static async Task CreateSchemaAsync(IDbConnection connection)
    {
        await connection.ExecuteAsync("""
            CREATE TABLE background_tasks
            (
                id BLOB NOT NULL PRIMARY KEY CHECK(length(id) = 16),
                task_id BLOB NOT NULL CHECK(length(task_id) = 16),
                type INTEGER NOT NULL,
                status INTEGER NOT NULL,
                upload_id BLOB NULL CHECK(upload_id IS NULL OR length(upload_id) = 16),
                queued_at_utc TEXT NOT NULL,
                started_at_utc TEXT NULL,
                completed_at_utc TEXT NULL,
                failed_at_utc TEXT NULL,
                error_message TEXT NULL,
                result_summary TEXT NULL,
                created_utc TEXT NOT NULL,
                updated_utc TEXT NULL,
                row_version BLOB NOT NULL CHECK(length(row_version) = 16)
            );
            """);
    }

    private static async Task InsertTaskAsync(
        IDbConnection connection,
        BackgroundTaskStatus status,
        Guid? taskId = null)
    {
        var nowUtc = DateTime.UtcNow;

        await connection.ExecuteAsync("""
            INSERT INTO background_tasks
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
                1,
                @Status,
                NULL,
                @QueuedAtUtc,
                @StartedAtUtc,
                @CompletedAtUtc,
                @FailedAtUtc,
                NULL,
                @ResultSummary,
                @CreatedUtc,
                @UpdatedUtc,
                @RowVersion
            );
            """,
            new
            {
                Id = Guid.NewGuid(),
                TaskId = taskId ?? Guid.NewGuid(),
                Status = (short)status,
                QueuedAtUtc = nowUtc,
                StartedAtUtc = status == BackgroundTaskStatus.Running ? nowUtc : null as DateTime?,
                CompletedAtUtc = status == BackgroundTaskStatus.Completed ? nowUtc : null as DateTime?,
                FailedAtUtc = status == BackgroundTaskStatus.Failed ? nowUtc : null as DateTime?,
                ResultSummary = status == BackgroundTaskStatus.Completed ? "ok" : null,
                CreatedUtc = nowUtc,
                UpdatedUtc = nowUtc,
                RowVersion = Guid.NewGuid()
            });
    }

    private sealed class TestConnectionFactory : IDbConnectionFactory
    {
        private readonly string _connectionString;

        public TestConnectionFactory(string connectionString)
        {
            _connectionString = connectionString;
        }

        public IDbConnection CreateConnection()
        {
            return new SqliteConnection(_connectionString);
        }
    }
}
