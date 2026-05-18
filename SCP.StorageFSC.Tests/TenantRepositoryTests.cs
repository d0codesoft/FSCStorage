using Dapper;
using Microsoft.Data.Sqlite;
using scp.filestorage.Data.Handlers;
using SCP.StorageFSC.Data;
using SCP.StorageFSC.Data.Repositories;
using System.Data;

namespace SCP.StorageFSC.Tests;

public sealed class TenantRepositoryTests
{
    [Fact]
    public async Task RecalculateTotalSizeBytesAsync_UpdatesTenantUsageFromActiveFiles()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        DapperTypeHandlers.Register();

        var databasePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.db");
        var connectionString = $"Data Source={databasePath};Pooling=False";
        var tenantId = Guid.NewGuid();
        var activeFileA = Guid.NewGuid();
        var activeFileB = Guid.NewGuid();
        var deletedFile = Guid.NewGuid();

        try
        {
            await using (var connection = new SqliteConnection(connectionString))
            {
                await connection.OpenAsync(cancellationToken);
                await CreateSchemaAsync(connection);

                await connection.ExecuteAsync("""
                    INSERT INTO tenants (id, user_id, external_tenant_id, name, is_active, total_size_bytes, row_version)
                    VALUES (@TenantId, @UserId, @TenantGuid, 'Tenant A', 1, 0, @TenantRowVersion);

                    INSERT INTO stored_files (id, file_size, is_deleted)
                    VALUES
                    (@ActiveFileA, 100, 0),
                    (@ActiveFileB, 250, 0),
                    (@DeletedFile, 999, 1);
                    """,
                    new
                    {
                        TenantId = tenantId,
                        UserId = Guid.NewGuid(),
                        TenantGuid = Guid.NewGuid(),
                        TenantRowVersion = Guid.NewGuid(),
                        ActiveFileA = activeFileA,
                        ActiveFileB = activeFileB,
                        DeletedFile = deletedFile
                    });

                await connection.ExecuteAsync("""
                    INSERT INTO tenant_files (id, tenant_id, stored_file_id, file_guid, file_name, is_active, row_version, created_utc)
                    VALUES
                    (@TenantFileA, @TenantId, @ActiveFileA, @FileGuidA, 'small.bin', 1, @RowVersionA, @NowUtc),
                    (@TenantFileB, @TenantId, @ActiveFileB, @FileGuidB, 'large.bin', 1, @RowVersionB, @NowUtc),
                    (@TenantFileDeleted, @TenantId, @DeletedFile, @FileGuidDeleted, 'deleted.bin', 1, @RowVersionDeleted, @NowUtc),
                    (@TenantFileInactive, @TenantId, @ActiveFileB, @FileGuidInactive, 'inactive.bin', 0, @RowVersionInactive, @NowUtc);
                    """,
                    new
                    {
                        TenantId = tenantId,
                        ActiveFileA = activeFileA,
                        ActiveFileB = activeFileB,
                        DeletedFile = deletedFile,
                        TenantFileA = Guid.NewGuid(),
                        TenantFileB = Guid.NewGuid(),
                        TenantFileDeleted = Guid.NewGuid(),
                        TenantFileInactive = Guid.NewGuid(),
                        FileGuidA = Guid.NewGuid(),
                        FileGuidB = Guid.NewGuid(),
                        FileGuidDeleted = Guid.NewGuid(),
                        FileGuidInactive = Guid.NewGuid(),
                        RowVersionA = Guid.NewGuid(),
                        RowVersionB = Guid.NewGuid(),
                        RowVersionDeleted = Guid.NewGuid(),
                        RowVersionInactive = Guid.NewGuid(),
                        NowUtc = DateTime.UtcNow
                    });
            }

            var repository = new TenantRepository(new TestConnectionFactory(connectionString));

            var updated = await repository.RecalculateTotalSizeBytesAsync(tenantId, cancellationToken);
            var tenant = await repository.GetByIdAsync(tenantId, cancellationToken);

            Assert.True(updated);
            Assert.NotNull(tenant);
            Assert.Equal(350, tenant!.TotalSizeBytes);
        }
        finally
        {
            SqliteConnection.ClearAllPools();

            if (File.Exists(databasePath))
                File.Delete(databasePath);
        }
    }

    [Fact]
    public async Task RecalculateAllTotalSizeBytesAsync_UpdatesAllTenants()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        DapperTypeHandlers.Register();

        var databasePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.db");
        var connectionString = $"Data Source={databasePath};Pooling=False";
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var fileA = Guid.NewGuid();
        var fileB = Guid.NewGuid();

        try
        {
            await using (var connection = new SqliteConnection(connectionString))
            {
                await connection.OpenAsync(cancellationToken);
                await CreateSchemaAsync(connection);

                await connection.ExecuteAsync("""
                    INSERT INTO tenants (id, user_id, external_tenant_id, name, is_active, total_size_bytes, row_version)
                    VALUES
                    (@TenantA, @UserA, @TenantGuidA, 'Tenant A', 1, 0, @TenantRowVersionA),
                    (@TenantB, @UserB, @TenantGuidB, 'Tenant B', 1, 0, @TenantRowVersionB);

                    INSERT INTO stored_files (id, file_size, is_deleted)
                    VALUES
                    (@FileA, 100, 0),
                    (@FileB, 250, 0);
                    """,
                    new
                    {
                        TenantA = tenantA,
                        TenantB = tenantB,
                        UserA = Guid.NewGuid(),
                        UserB = Guid.NewGuid(),
                        TenantGuidA = Guid.NewGuid(),
                        TenantGuidB = Guid.NewGuid(),
                        TenantRowVersionA = Guid.NewGuid(),
                        TenantRowVersionB = Guid.NewGuid(),
                        FileA = fileA,
                        FileB = fileB
                    });

                await connection.ExecuteAsync("""
                    INSERT INTO tenant_files (id, tenant_id, stored_file_id, file_guid, file_name, is_active, row_version, created_utc)
                    VALUES
                    (@TenantFileA, @TenantA, @FileA, @FileGuidA, 'a.bin', 1, @RowVersionA, @NowUtc),
                    (@TenantFileB, @TenantB, @FileB, @FileGuidB, 'b.bin', 1, @RowVersionB, @NowUtc);
                    """,
                    new
                    {
                        TenantA = tenantA,
                        TenantB = tenantB,
                        FileA = fileA,
                        FileB = fileB,
                        TenantFileA = Guid.NewGuid(),
                        TenantFileB = Guid.NewGuid(),
                        FileGuidA = Guid.NewGuid(),
                        FileGuidB = Guid.NewGuid(),
                        RowVersionA = Guid.NewGuid(),
                        RowVersionB = Guid.NewGuid(),
                        NowUtc = DateTime.UtcNow
                    });
            }

            var repository = new TenantRepository(new TestConnectionFactory(connectionString));

            var updatedCount = await repository.RecalculateAllTotalSizeBytesAsync(cancellationToken);
            var tenantAResult = await repository.GetByIdAsync(tenantA, cancellationToken);
            var tenantBResult = await repository.GetByIdAsync(tenantB, cancellationToken);

            Assert.Equal(2, updatedCount);
            Assert.NotNull(tenantAResult);
            Assert.NotNull(tenantBResult);
            Assert.Equal(100, tenantAResult!.TotalSizeBytes);
            Assert.Equal(250, tenantBResult!.TotalSizeBytes);
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
            CREATE TABLE tenants
            (
                id BLOB NOT NULL PRIMARY KEY CHECK(length(id) = 16),
                user_id BLOB NOT NULL CHECK(length(user_id) = 16),
                external_tenant_id BLOB NOT NULL CHECK(length(external_tenant_id) = 16),
                name TEXT NOT NULL,
                is_active INTEGER NOT NULL,
                total_size_bytes INTEGER NOT NULL DEFAULT 0,
                created_utc TEXT NULL,
                updated_utc TEXT NULL,
                row_version BLOB NOT NULL CHECK(length(row_version) = 16)
            );

            CREATE TABLE stored_files
            (
                id BLOB NOT NULL PRIMARY KEY CHECK(length(id) = 16),
                file_size INTEGER NOT NULL,
                is_deleted INTEGER NOT NULL
            );

            CREATE TABLE tenant_files
            (
                id BLOB NOT NULL PRIMARY KEY CHECK(length(id) = 16),
                tenant_id BLOB NOT NULL CHECK(length(tenant_id) = 16),
                stored_file_id BLOB NOT NULL CHECK(length(stored_file_id) = 16),
                file_guid BLOB NOT NULL CHECK(length(file_guid) = 16),
                file_name TEXT NOT NULL,
                is_active INTEGER NOT NULL,
                row_version BLOB NOT NULL CHECK(length(row_version) = 16),
                created_utc TEXT NOT NULL,
                deleted_utc TEXT NULL
            );
            """);
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
