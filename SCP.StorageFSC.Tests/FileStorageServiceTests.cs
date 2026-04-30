using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SCP.StorageFSC.Data.Dto;
using SCP.StorageFSC.Data.Models;
using SCP.StorageFSC.Data.Repositories;
using SCP.StorageFSC.InterfacesService;
using SCP.StorageFSC.Security;
using SCP.StorageFSC.SecurityPermission;
using SCP.StorageFSC.Services;
using System.Security.Cryptography;
using System.Text;

namespace SCP.StorageFSC.Tests;

public sealed class FileStorageServiceTests : IDisposable
{
    private readonly string _dataPath = Path.Combine(Path.GetTempPath(), "scp-filestorage-tests", Guid.NewGuid().ToString("N"));
    private readonly InMemoryStoredFileRepository _storedFiles = new();
    private readonly InMemoryTenantFileRepository _tenantFiles = new();
    private readonly TestCurrentTenantAccessor _currentTenant = new();
    private readonly TestTenantAuthorizationService _authorization = new();

    public FileStorageServiceTests()
    {
        Directory.CreateDirectory(_dataPath);
    }

    [Fact]
    public async Task SaveFileAsync_StoresNewPhysicalFileAndCreatesTenantLink()
    {
        var sut = CreateService();
        var bytes = Encoding.UTF8.GetBytes("hello from file storage");

        var result = await sut.SaveFileAsync(new SaveFileRequest
        {
            FileName = "document.txt",
            ContentType = "text/plain",
            Category = "docs",
            ExternalKey = "doc-1",
            Content = new MemoryStream(bytes)
        });

        Assert.True(result.Success);
        Assert.Equal(SaveFileStatus.Success, result.Status);
        Assert.NotNull(result.File);
        Assert.Equal(_currentTenant.Current!.TenantId, result.File.TenantId);
        Assert.Equal("document.txt", result.File.FileName);
        Assert.Equal("text/plain", result.File.ContentType);
        Assert.Equal(bytes.Length, result.File.FileSize);

        var storedFile = Assert.Single(_storedFiles.Items);
        var expectedSha256 = Convert.ToHexString(SHA256.HashData(bytes));
        Assert.Equal(expectedSha256, storedFile.Sha256);
        Assert.Equal(1, storedFile.ReferenceCount);
        Assert.True(File.Exists(Path.Combine(_dataPath, storedFile.PhysicalPath)));

        var tenantFile = Assert.Single(_tenantFiles.Items);
        Assert.Equal(storedFile.Id, tenantFile.StoredFileId);
        Assert.Equal(result.File.FileGuid, tenantFile.FileGuid);
    }

    [Fact]
    public async Task SaveFileAsync_WhenContentAlreadyStored_ReusesStoredFileAndIncrementsReferenceCount()
    {
        var sut = CreateService();
        var bytes = Encoding.UTF8.GetBytes("same bytes");
        var existing = new StoredFile
        {
            Sha256 = Convert.ToHexString(SHA256.HashData(bytes)),
            Crc32 = Convert.ToHexString(System.IO.Hashing.Crc32.Hash(bytes)),
            FileSize = bytes.Length,
            PhysicalPath = "aa/bb/existing.txt",
            OriginalFileName = "existing.txt",
            ContentType = "text/plain",
            ReferenceCount = 1,
            CreatedUtc = DateTime.UtcNow
        };
        await _storedFiles.InsertAsync(existing);

        var result = await sut.SaveFileAsync(new SaveFileRequest
        {
            FileName = "copy.txt",
            Content = new MemoryStream(bytes)
        });

        Assert.True(result.Success);
        Assert.Single(_storedFiles.Items);
        Assert.Equal(2, existing.ReferenceCount);
        Assert.Single(_tenantFiles.Items);
        Assert.Equal(existing.Id, result.File!.StoredFileId);
        Assert.Empty(Directory.EnumerateFiles(_dataPath, ".tmp_*"));
    }

    [Fact]
    public async Task SaveFileAsync_WhenFileNameIsBlank_ReturnsValidationError()
    {
        var sut = CreateService();

        var result = await sut.SaveFileAsync(new SaveFileRequest
        {
            FileName = " ",
            Content = new MemoryStream([1, 2, 3])
        });

        Assert.False(result.Success);
        Assert.Equal(SaveFileStatus.ValidationError, result.Status);
        Assert.Equal("validation.file_name_required", result.ErrorCode);
        Assert.Empty(_storedFiles.Items);
        Assert.Empty(_tenantFiles.Items);
    }

    [Fact]
    public async Task OpenReadAsync_WhenFileBelongsToCurrentTenant_ReturnsReadableContent()
    {
        var sut = CreateService();
        var saveResult = await sut.SaveFileAsync(new SaveFileRequest
        {
            FileName = "open.txt",
            Content = new MemoryStream(Encoding.UTF8.GetBytes("read me"))
        });

        await using var opened = await sut.OpenReadAsync(saveResult.File!.FileGuid);

        Assert.NotNull(opened);
        using var reader = new StreamReader(opened!.Content, Encoding.UTF8);
        Assert.Equal("read me", await reader.ReadToEndAsync());
    }

    [Fact]
    public async Task DeleteFileAsync_WhenLastReference_RemovesPhysicalFileAndStoredRow()
    {
        var sut = CreateService();
        var saveResult = await sut.SaveFileAsync(new SaveFileRequest
        {
            FileName = "delete.txt",
            Content = new MemoryStream(Encoding.UTF8.GetBytes("delete me"))
        });
        var storedFile = Assert.Single(_storedFiles.Items);
        var fullPath = Path.Combine(_dataPath, storedFile.PhysicalPath);

        var deleted = await sut.DeleteFileAsync(saveResult.File!.FileGuid);

        Assert.True(deleted);
        Assert.Empty(_storedFiles.Items);
        Assert.False(File.Exists(fullPath));
        Assert.True(Assert.Single(_tenantFiles.Items).DeletedUtc.HasValue);
    }

    [Fact]
    public async Task GetFilesAsync_FiltersDeletedOrMissingStoredFiles()
    {
        var sut = CreateService();
        var visible = await InsertTenantFileAsync(storedFileDeleted: false);
        await InsertTenantFileAsync(storedFileDeleted: true);
        _tenantFiles.Add(new TenantFile
        {
            Id = Guid.NewGuid(),
            TenantId = _currentTenant.Current!.TenantId!.Value,
            StoredFileId = Guid.NewGuid(),
            FileGuid = Guid.NewGuid(),
            FileName = "missing.txt",
            IsActive = true,
            CreatedUtc = DateTime.UtcNow
        });

        var files = await sut.GetFilesAsync();

        var file = Assert.Single(files);
        Assert.Equal(visible.FileGuid, file.FileGuid);
    }

    [Fact]
    public async Task ServiceMethods_DemandExpectedTenantPermissions()
    {
        var sut = CreateService();

        await sut.SaveFileAsync(new SaveFileRequest
        {
            FileName = "permissions.txt",
            Content = new MemoryStream(Encoding.UTF8.GetBytes("permissions"))
        });
        await sut.GetFilesAsync();
        await sut.DeleteFileAsync(Guid.NewGuid());

        Assert.Contains(TenantPermission.Write, _authorization.DemandedPermissions);
        Assert.Contains(TenantPermission.Read, _authorization.DemandedPermissions);
        Assert.Contains(TenantPermission.Delete, _authorization.DemandedPermissions);
    }

    [Fact]
    public async Task DeleteOrphanFilesAsync_WhenCurrentTokenIsNotAdmin_ThrowsUnauthorizedAccessException()
    {
        var sut = CreateService();
        _currentTenant.Current!.IsAdmin = false;

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => sut.DeleteOrphanFilesAsync());
    }

    public void Dispose()
    {
        if (Directory.Exists(_dataPath))
        {
            Directory.Delete(_dataPath, recursive: true);
        }
    }

    private FileStorageService CreateService()
    {
        return new FileStorageService(
            _tenantFiles,
            _storedFiles,
            _currentTenant,
            _authorization,
            Options.Create(new FileStorageOptions
            {
                BasePath = _dataPath,
                DataPath = _dataPath,
                LogsPath = Path.Combine(_dataPath, "logs")
            }),
            NullLogger<FileStorageService>.Instance);
    }

    private async Task<TenantFile> InsertTenantFileAsync(bool storedFileDeleted)
    {
        var storedFile = new StoredFile
        {
            Sha256 = Guid.NewGuid().ToString("N"),
            Crc32 = Guid.NewGuid().ToString("N")[..8],
            FileSize = 10,
            PhysicalPath = Guid.NewGuid().ToString("N"),
            OriginalFileName = "file.txt",
            ReferenceCount = 1,
            IsDeleted = storedFileDeleted,
            CreatedUtc = DateTime.UtcNow
        };
        await _storedFiles.InsertAsync(storedFile);

        var tenantFile = new TenantFile
        {
            TenantId = _currentTenant.Current!.TenantId!.Value,
            StoredFileId = storedFile.Id,
            FileGuid = Guid.NewGuid(),
            FileName = "file.txt",
            IsActive = true,
            CreatedUtc = DateTime.UtcNow
        };
        await _tenantFiles.InsertAsync(tenantFile);

        return tenantFile;
    }

    private sealed class TestCurrentTenantAccessor : ICurrentTenantAccessor
    {
        public CurrentTenantContext? Current { get; set; } = new()
        {
            TenantId = Guid.NewGuid(),
            TenantGuid = Guid.NewGuid(),
            TenantName = "Test tenant",
            TokenId = Guid.NewGuid(),
            TokenName = "Tests",
            CanRead = true,
            CanWrite = true,
            CanDelete = true
        };

        public CurrentTenantContext GetRequired()
        {
            return Current ?? throw new UnauthorizedAccessException("Current tenant is required.");
        }
    }

    private sealed class TestTenantAuthorizationService : ITenantAuthorizationService
    {
        public List<TenantPermission> DemandedPermissions { get; } = [];

        public CurrentTenantContext GetRequiredCurrentTenant()
        {
            return new CurrentTenantContext();
        }

        public void DemandAuthenticated()
        {
        }

        public void DemandAdmin()
        {
        }

        public void DemandPermission(TenantPermission permission)
        {
            DemandedPermissions.Add(permission);
        }

        public Task DemandAdminOrSameTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task DemandAdminOrSameTenantGuidAsync(Guid tenantGuid, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryStoredFileRepository : IStoredFileRepository
    {
        private readonly List<StoredFile> _items = [];
        public IReadOnlyList<StoredFile> Items => _items;

        public Task<Guid> InsertAsync(StoredFile file, CancellationToken cancellationToken = default)
        {
            _items.Add(file);
            return Task.FromResult(file.Id);
        }

        public Task<StoredFile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_items.FirstOrDefault(file => file.Id == id));
        }

        public Task<StoredFile?> GetBySha256Async(string sha256, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_items.FirstOrDefault(file => file.Sha256 == sha256 && !file.IsDeleted));
        }

        public Task<StoredFile?> GetByHashesAsync(string sha256, string crc32, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_items.FirstOrDefault(file => file.Sha256 == sha256 && file.Crc32 == crc32 && !file.IsDeleted));
        }

        public Task<IReadOnlyList<StoredFile>> GetActiveAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<StoredFile>>(
                _items.Where(file => !file.IsDeleted).ToList());
        }

        public Task<bool> IncrementReferenceCountAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var item = _items.FirstOrDefault(file => file.Id == id);
            if (item is null)
                return Task.FromResult(false);

            item.ReferenceCount++;
            return Task.FromResult(true);
        }

        public Task<bool> DecrementReferenceCountAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var item = _items.FirstOrDefault(file => file.Id == id);
            if (item is null)
                return Task.FromResult(false);

            item.ReferenceCount--;
            return Task.FromResult(true);
        }

        public Task<IReadOnlyList<StoredFile>> GetOrphanFilesAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<StoredFile>>(_items.Where(file => file.ReferenceCount <= 0 && !file.IsDeleted).ToList());
        }

        public Task<bool> MarkDeletedAsync(Guid id, DateTime deletedUtc, CancellationToken cancellationToken = default)
        {
            var item = _items.FirstOrDefault(file => file.Id == id);
            if (item is null)
                return Task.FromResult(false);

            item.IsDeleted = true;
            item.DeletedUtc = deletedUtc;
            return Task.FromResult(true);
        }

        public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_items.RemoveAll(file => file.Id == id) > 0);
        }
    }

    private sealed class InMemoryTenantFileRepository : ITenantFileRepository
    {
        private readonly List<TenantFile> _items = [];
        public IReadOnlyList<TenantFile> Items => _items;

        public void Add(TenantFile tenantFile)
        {
            _items.Add(tenantFile);
        }

        public Task<Guid> InsertAsync(TenantFile tenantFile, CancellationToken cancellationToken = default)
        {
            _items.Add(tenantFile);
            return Task.FromResult(tenantFile.Id);
        }

        public Task<TenantFile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_items.FirstOrDefault(file => file.Id == id));
        }

        public Task<TenantFile?> GetByFileGuidAsync(Guid fileGuid, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_items.FirstOrDefault(file => file.FileGuid == fileGuid && file.IsActive));
        }

        public Task<TenantFile?> GetByTenantAndFileGuidAsync(Guid tenantId, Guid fileGuid, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_items.FirstOrDefault(file => file.TenantId == tenantId && file.FileGuid == fileGuid && file.IsActive));
        }

        public Task<TenantFile?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_items.FirstOrDefault(file => file.FileName == name && file.IsActive));
        }

        public Task<IReadOnlyList<TenantFile>> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<TenantFile>>(_items.Where(file => file.TenantId == tenantId && file.IsActive).ToList());
        }

        public Task<IReadOnlyList<TenantFile>> GetByStoredFileIdAsync(Guid storedFileId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<TenantFile>>(_items.Where(file => file.StoredFileId == storedFileId && file.IsActive).ToList());
        }

        public Task<bool> SoftDeleteAsync(Guid id, DateTime deletedUtc, CancellationToken cancellationToken = default)
        {
            var item = _items.FirstOrDefault(file => file.Id == id);
            if (item is null)
                return Task.FromResult(false);

            item.IsActive = false;
            item.DeletedUtc = deletedUtc;
            return Task.FromResult(true);
        }

        public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_items.RemoveAll(file => file.Id == id) > 0);
        }

        public Task<TenantFile?> GetByTenantAndExternalKeyAsync(Guid tenantId, string externalKey, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_items.FirstOrDefault(file => file.TenantId == tenantId && file.ExternalKey == externalKey && file.IsActive));
        }
    }
}
