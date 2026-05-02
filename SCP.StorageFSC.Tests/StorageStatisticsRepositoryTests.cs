using Dapper;
using Microsoft.Data.Sqlite;
using scp.filestorage.Data.Handlers;
using SCP.StorageFSC.Data;
using SCP.StorageFSC.Data.Repositories;
using System.Data;

namespace SCP.StorageFSC.Tests;

public sealed class StorageStatisticsRepositoryTests
{
    [Fact]
    public async Task GetAsync_ReturnsStorageTenantAndLargestFileStatistics()
    {
        DapperTypeHandlers.Register();

        var databasePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.db");
        var connectionString = $"Data Source={databasePath};Pooling=False";
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        try
        {
            await using (var connection = new SqliteConnection(connectionString))
            {
                await connection.OpenAsync();
                await CreateSchemaAsync(connection);

                var tenantAGuid = Guid.NewGuid();
                var tenantBGuid = Guid.NewGuid();
                var fileA = Guid.NewGuid();
                var fileB = Guid.NewGuid();
                var deletedFile = Guid.NewGuid();

                await connection.ExecuteAsync("""
                    INSERT INTO tenants (id, external_tenant_id, name, is_active)
                    VALUES
                    (@TenantA, @TenantAGuid, 'Tenant A', 1),
                    (@TenantB, @TenantBGuid, 'Tenant B', 1);

                    INSERT INTO stored_files (id, file_size, is_deleted)
                    VALUES
                    (@FileA, 100, 0),
                    (@FileB, 250, 0),
                    (@DeletedFile, 999, 1);
                    """,
                    new
                    {
                        TenantA = tenantA,
                        TenantAGuid = tenantAGuid,
                        TenantB = tenantB,
                        TenantBGuid = tenantBGuid,
                        FileA = fileA,
                        FileB = fileB,
                        DeletedFile = deletedFile
                    });

                await connection.ExecuteAsync("""
                    INSERT INTO tenant_files
                    (
                        id,
                        tenant_id,
                        stored_file_id,
                        file_guid,
                        file_name,
                        is_active,
                        created_utc
                    )
                    VALUES
                    (@TenantFileA, @TenantA, @FileA, @FileGuidA, 'small.bin', 1, @NowUtc),
                    (@TenantFileB, @TenantB, @FileB, @FileGuidB, 'large.bin', 1, @NowUtc),
                    (@TenantFileDeleted, @TenantB, @DeletedFile, @FileGuidDeleted, 'deleted.bin', 1, @NowUtc),
                    (@TenantFileInactive, @TenantA, @FileB, @FileGuidInactive, 'inactive.bin', 0, @NowUtc);
                    """,
                    new
                    {
                        TenantFileA = Guid.NewGuid(),
                        TenantFileB = Guid.NewGuid(),
                        TenantFileDeleted = Guid.NewGuid(),
                        TenantFileInactive = Guid.NewGuid(),
                        TenantA = tenantA,
                        TenantB = tenantB,
                        FileA = fileA,
                        FileB = fileB,
                        DeletedFile = deletedFile,
                        FileGuidA = Guid.NewGuid(),
                        FileGuidB = Guid.NewGuid(),
                        FileGuidDeleted = Guid.NewGuid(),
                        FileGuidInactive = Guid.NewGuid(),
                        NowUtc = DateTime.UtcNow
                    });
            }

            var repository = new StorageStatisticsRepository(new TestConnectionFactory(connectionString));

            var result = await repository.GetAsync(largestFilesLimit: 10);

            Assert.Equal(350, result.UsedBytes);
            Assert.Equal(2, result.StoredFileCount);
            Assert.Equal(2, result.TenantFileCount);
            Assert.Equal(2, result.TenantCount);
            Assert.Equal("large.bin", result.LargestFiles[0].FileName);

            var tenantAStats = Assert.Single(result.Tenants, tenant => tenant.TenantId == tenantA);
            Assert.Equal(1, tenantAStats.FileCount);
            Assert.Equal(100, tenantAStats.UsedBytes);

            var tenantBStats = Assert.Single(result.Tenants, tenant => tenant.TenantId == tenantB);
            Assert.Equal(1, tenantBStats.FileCount);
            Assert.Equal(250, tenantBStats.UsedBytes);
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
                external_tenant_id BLOB NOT NULL CHECK(length(external_tenant_id) = 16),
                name TEXT NOT NULL,
                is_active INTEGER NOT NULL
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
                category TEXT NULL,
                external_key TEXT NULL,
                is_active INTEGER NOT NULL,
                created_utc TEXT NOT NULL
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
