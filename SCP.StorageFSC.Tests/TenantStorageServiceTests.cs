using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using scp.filestorage.Data.Models;
using SCP.StorageFSC.Data.Dto;
using SCP.StorageFSC.Data.Models;
using SCP.StorageFSC.Data.Repositories;
using SCP.StorageFSC.Services;
using System.Security.Claims;
using scp.filestorage.Data.Repositories;
using scp.filestorage.Services.Auth;

namespace SCP.StorageFSC.Tests;

public sealed class TenantStorageServiceTests
{
    private readonly InMemoryTenantRepository _tenants = new();
    private readonly InMemoryApiTokenRepository _tokens = new();
    private readonly InMemoryUserRepository _users = new();
    private readonly InMemoryUserRoleRepository _userRoles = new();
    private readonly InMemoryTenantFileRepository _tenantFiles = new();
    private readonly InMemoryStoredFileRepository _storedFiles = new();
    private readonly InMemoryDeletedTenantRepository _deletedTenants = new();
    private readonly HttpContextAccessor _httpContextAccessor = new();

    [Fact]
    public async Task GetTenantsAsync_WhenUserIsNotAdmin_ReturnsOnlyCurrentUsersTenants()
    {
        var currentUserId = Guid.CreateVersion7();
        await _tenants.InsertAsync(new Tenant
        {
            UserId = currentUserId,
            Name = "Mine",
            ExternalTenantId = Guid.CreateVersion7(),
            IsActive = true
        });
        await _tenants.InsertAsync(new Tenant
        {
            UserId = Guid.CreateVersion7(),
            Name = "Other",
            ExternalTenantId = Guid.CreateVersion7(),
            IsActive = true
        });

        SetCurrentUser(currentUserId, isAdmin: false);
        var sut = CreateService();

        var result = await sut.GetTenantsAsync();

        var tenant = Assert.Single(result);
        Assert.Equal("Mine", tenant.Name);
        Assert.Equal(currentUserId, tenant.UserId);
    }

    [Fact]
    public async Task GetTenantTokensAsync_WhenUserIsNotAdmin_ReturnsOnlyCurrentUsersTokens()
    {
        var currentUserId = Guid.CreateVersion7();
        var otherUserId = Guid.CreateVersion7();
        var tenant = new Tenant
        {
            UserId = currentUserId,
            Name = "Mine",
            ExternalTenantId = Guid.CreateVersion7(),
            IsActive = true
        };
        await _tenants.InsertAsync(tenant);

        await _tokens.InsertAsync(new ApiToken
        {
            UserId = currentUserId,
            TenantId = tenant.Id,
            Name = "Current user key",
            TokenHash = "hash1",
            TokenPrefix = "prefix1",
            IsActive = true,
            CanRead = true
        });

        await _tokens.InsertAsync(new ApiToken
        {
            UserId = otherUserId,
            TenantId = tenant.Id,
            Name = "Other user key",
            TokenHash = "hash2",
            TokenPrefix = "prefix2",
            IsActive = true,
            CanRead = true
        });

        SetCurrentUser(currentUserId, isAdmin: false);
        var sut = CreateService();

        var result = await sut.GetTenantTokensAsync(tenant.Id);

        var token = Assert.Single(result);
        Assert.Equal("Current user key", token.Name);
        Assert.Equal(currentUserId, token.UserId);
    }

    [Fact]
    public async Task GetUsersWithTenantsAsync_WhenUserIsAdmin_ReturnsUsersWithTheirTenants()
    {
        var firstUser = new User
        {
            Id = Guid.CreateVersion7(),
            Name = "Alice",
            NormalizedName = "ALICE",
            PasswordHash = "hash"
        };
        var secondUser = new User
        {
            Id = Guid.CreateVersion7(),
            Name = "Bob",
            NormalizedName = "BOB",
            PasswordHash = "hash"
        };
        await _users.InsertAsync(firstUser);
        await _users.InsertAsync(secondUser);

        await _tenants.InsertAsync(new Tenant
        {
            UserId = firstUser.Id,
            Name = "Alice tenant",
            ExternalTenantId = Guid.CreateVersion7(),
            IsActive = true
        });

        SetCurrentUser(Guid.CreateVersion7(), isAdmin: true);
        var sut = CreateService();

        var result = await sut.GetUsersWithTenantsAsync();

        Assert.Equal(2, result.Count);
        Assert.Contains(result, item => item.UserName == "Alice" && item.Tenants.Count == 1);
        Assert.Contains(result, item => item.UserName == "Bob" && item.Tenants.Count == 0);
    }

    [Fact]
    public async Task UpdateTenantAsync_ChangesNameAndStatus()
    {
        var tenant = new Tenant
        {
            UserId = Guid.CreateVersion7(),
            Name = "Alpha",
            ExternalTenantId = Guid.CreateVersion7(),
            IsActive = true
        };
        await _tenants.InsertAsync(tenant);

        SetCurrentUser(Guid.CreateVersion7(), isAdmin: true);
        var sut = CreateService();

        var result = await sut.UpdateTenantAsync(tenant.Id, new UpdateTenantRequest
        {
            Name = "Beta",
            IsActive = false
        });

        Assert.NotNull(result);
        Assert.Equal("Beta", result!.Name);
        Assert.False(result.IsActive);
        Assert.Equal("Beta", tenant.Name);
        Assert.False(tenant.IsActive);
        Assert.NotNull(tenant.UpdatedUtc);
    }

    [Fact]
    public async Task DeleteTenantAsync_RemovesTenant()
    {
        var tenant = new Tenant
        {
            UserId = Guid.CreateVersion7(),
            Name = "Delete me",
            ExternalTenantId = Guid.CreateVersion7(),
            IsActive = true
        };
        await _tenants.InsertAsync(tenant);

        SetCurrentUser(Guid.CreateVersion7(), isAdmin: true);
        var sut = CreateService();

        var deleted = await sut.DeleteTenantAsync(tenant.Id);

        Assert.True(deleted);
        Assert.Null(await _tenants.GetByIdAsync(tenant.Id));
    }

    [Fact]
    public async Task CreateTenantAsync_AssignsTenantToRequestedUser()
    {
        var owner = new User
        {
            Id = Guid.CreateVersion7(),
            Name = "Owner",
            NormalizedName = "OWNER",
            PasswordHash = "hash"
        };
        await _users.InsertAsync(owner);
        SetCurrentUser(Guid.CreateVersion7(), isAdmin: true);

        var sut = CreateService();

        var result = await sut.CreateTenantAsync(new CreateTenantRequest
        {
            UserId = owner.Id,
            Name = "Tenant A"
        });

        Assert.Equal(owner.Id, result.UserId);
        Assert.Equal(owner.Id, (await _tenants.GetByIdAsync(result.Id))!.UserId);
    }

    [Fact]
    public async Task DeleteUserAsync_RemovesOwnedTenantsAndQueuesDeletedTenantRecords()
    {
        var owner = new User
        {
            Id = Guid.CreateVersion7(),
            Name = "Owner",
            NormalizedName = "OWNER",
            PasswordHash = "hash"
        };
        await _users.InsertAsync(owner);
        await _userRoles.InsertAsync(new UserRole
        {
            UserId = Guid.CreateVersion7(),
            RoleId = scp.filestorage.Data.Models.SystemRoles.AdministratorId,
            CreatedUtc = DateTime.UtcNow
        });

        var tenant = new Tenant
        {
            UserId = owner.Id,
            Name = "Owned tenant",
            ExternalTenantId = Guid.CreateVersion7(),
            IsActive = true
        };
        await _tenants.InsertAsync(tenant);
        await _tenantFiles.InsertAsync(new TenantFile
        {
            TenantId = tenant.Id,
            StoredFileId = Guid.CreateVersion7(),
            FileGuid = Guid.CreateVersion7(),
            FileName = "file.bin",
            IsActive = true,
            CreatedUtc = DateTime.UtcNow
        });
        await _storedFiles.InsertAsync(new StoredFile
        {
            Id = _tenantFiles.Items[0].StoredFileId,
            Sha256 = "hash",
            Crc32 = "crc32",
            FileSize = 1,
            PhysicalPath = "path",
            OriginalFileName = "file.bin",
            ReferenceCount = 1,
            CreatedUtc = DateTime.UtcNow
        });

        SetCurrentUser(Guid.CreateVersion7(), isAdmin: true);
        var sut = CreateService();

        var deleted = await sut.DeleteUserAsync(owner.Id);

        Assert.True(deleted);
        Assert.Null(await _users.GetByIdAsync(owner.Id));
        Assert.Null(await _tenants.GetByIdAsync(tenant.Id));
        Assert.Single(_deletedTenants.Items);
        Assert.Equal(0, _storedFiles.Items[0].ReferenceCount);
    }

    [Fact]
    public async Task UpdateApiTokenAsync_ChangesPermissionsAndActivation()
    {
        var tenant = new Tenant
        {
            UserId = Guid.CreateVersion7(),
            Name = "Tenant",
            ExternalTenantId = Guid.CreateVersion7(),
            IsActive = true
        };
        await _tenants.InsertAsync(tenant);

        var token = new ApiToken
        {
            UserId = Guid.CreateVersion7(),
            TenantId = tenant.Id,
            Name = "Reader",
            TokenHash = "hash",
            TokenPrefix = "prefix",
            IsActive = true,
            CanRead = true
        };
        await _tokens.InsertAsync(token);

        SetCurrentUser(Guid.CreateVersion7(), isAdmin: true);
        var sut = CreateService();
        var expiresUtc = DateTime.UtcNow.AddDays(7);

        var result = await sut.UpdateApiTokenAsync(token.Id, new UpdateApiTokenRequest
        {
            Name = "Writer",
            CanRead = true,
            CanWrite = true,
            CanDelete = true,
            IsAdmin = true,
            IsActive = false,
            ExpiresUtc = expiresUtc
        });

        Assert.NotNull(result);
        Assert.Equal("Writer", result!.Name);
        Assert.True(result.CanWrite);
        Assert.True(result.CanDelete);
        Assert.True(result.IsAdmin);
        Assert.False(result.IsActive);
        Assert.Equal(expiresUtc, result.ExpiresUtc);
        Assert.NotNull(result.RevokedUtc);
    }

    [Fact]
    public async Task CreateTenantAsync_WhenCurrentSessionIsNotAdmin_ThrowsUnauthorizedAccessException()
    {
        SetCurrentUser(Guid.CreateVersion7(), isAdmin: false);
        var sut = CreateService();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            sut.CreateTenantAsync(new CreateTenantRequest
            {
                Name = "Forbidden"
            }));
    }

    private TenantStorageService CreateService()
    {
        return new TenantStorageService(
            _tenants,
            _tokens,
            _users,
            _userRoles,
            _tenantFiles,
            _storedFiles,
            _deletedTenants,
            new TestPasswordHashService(),
            _httpContextAccessor,
            NullLogger<TenantStorageService>.Instance);
    }

    private void SetCurrentUser(Guid userId, bool isAdmin)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new("auth_type", "web_user")
        };

        if (isAdmin)
            claims.Add(new Claim(ClaimTypes.Role, "Admin"));

        _httpContextAccessor.HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "FscCookie"))
        };
    }

    private sealed class InMemoryTenantRepository : ITenantRepository
    {
        private readonly List<Tenant> _items = [];

        public Task<bool> InsertAsync(Tenant tenant, CancellationToken cancellationToken = default)
        {
            _items.Add(tenant);
            return Task.FromResult(true);
        }

        public Task<Tenant?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_items.FirstOrDefault(item => item.Id == id));
        }

        public Task<Tenant?> GetByGuidAsync(Guid tenantGuid, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_items.FirstOrDefault(item => item.ExternalTenantId == tenantGuid));
        }

        public Task<Tenant?> GetFirstByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_items.FirstOrDefault(item => item.UserId == userId));
        }

        public Task<IReadOnlyList<Tenant>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<Tenant>>(_items.Where(item => item.UserId == userId).ToList());
        }

        public Task<Tenant?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_items.FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.Ordinal)));
        }

        public Task<IReadOnlyList<Tenant>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<Tenant>>(_items.ToList());
        }

        public Task<bool> UpdateAsync(Tenant tenant, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_items.Any(item => item.Id == tenant.Id));
        }

        public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_items.RemoveAll(item => item.Id == id) > 0);
        }
    }

    private sealed class InMemoryApiTokenRepository : IApiTokenRepository
    {
        private readonly List<ApiToken> _items = [];

        public Task<Guid> InsertAsync(ApiToken token, CancellationToken cancellationToken = default)
        {
            _items.Add(token);
            return Task.FromResult(token.Id);
        }

        public Task<ApiToken?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_items.FirstOrDefault(item => item.Id == id));
        }

        public Task<ApiToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_items.FirstOrDefault(item => item.TokenHash == tokenHash));
        }

        public Task<ApiToken?> GetFirstByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_items.FirstOrDefault(item => item.UserId == userId));
        }

        public Task<IReadOnlyList<ApiToken>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<ApiToken>>(_items.Where(item => item.UserId == userId).ToList());
        }

        public Task<IReadOnlyList<ApiToken>> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<ApiToken>>(_items.Where(item => item.TenantId == tenantId).ToList());
        }

        public Task<IReadOnlyList<ApiToken>> GetByTenantIdAndUserIdAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<ApiToken>>(_items.Where(item => item.TenantId == tenantId && item.UserId == userId).ToList());
        }

        public Task<bool> UpdateAsync(ApiToken token, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_items.Any(item => item.Id == token.Id));
        }

        public Task<bool> UpdateLastUsedAsync(Guid id, DateTime lastUsedUtc, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_items.Any(item => item.Id == id));
        }

        public Task<bool> UpdateLastUsedAsync(ApiToken token, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_items.Any(item => item.Id == token.Id));
        }

        public Task<bool> RevokeAsync(Guid id, DateTime revokedUtc, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_items.Any(item => item.Id == id));
        }

        public Task<bool> RevokeAsync(ApiToken token, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_items.Any(item => item.Id == token.Id));
        }

        public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_items.RemoveAll(item => item.Id == id) > 0);
        }

        public Task<bool> HasAnyAdminTokenAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_items.Any(item => item.IsAdmin));
        }

        public Task<ApiToken?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_items.FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.Ordinal)));
        }
    }

    private sealed class InMemoryUserRepository : IUserRepository
    {
        private readonly List<User> _items = [];

        public Task<bool> InsertAsync(User user, CancellationToken cancellationToken = default)
        {
            _items.Add(user);
            return Task.FromResult(true);
        }

        public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_items.FirstOrDefault(item => item.Id == id));
        }

        public Task<User?> GetByNormalizedNameAsync(string normalizedName, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_items.FirstOrDefault(item => item.NormalizedName == normalizedName));
        }

        public Task<User?> GetByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_items.FirstOrDefault(item => item.NormalizedEmail == normalizedEmail));
        }

        public Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<User>>(_items.ToList());
        }

        public Task<bool> UpdateAsync(User user, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_items.Any(item => item.Id == user.Id));
        }

        public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_items.RemoveAll(item => item.Id == id) > 0);
        }
    }

    private sealed class InMemoryUserRoleRepository : IUserRoleRepository
    {
        private readonly List<UserRole> _items = [];

        public Task<bool> InsertAsync(UserRole userRole, CancellationToken cancellationToken = default)
        {
            _items.Add(userRole);
            return Task.FromResult(true);
        }

        public Task<UserRole?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(_items.FirstOrDefault(x => x.Id == id));

        public Task<UserRole?> GetByUserIdAndRoleIdAsync(Guid userId, Guid roleId, CancellationToken cancellationToken = default)
            => Task.FromResult(_items.FirstOrDefault(x => x.UserId == userId && x.RoleId == roleId));

        public Task<IReadOnlyList<UserRole>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<UserRole>>(_items.Where(x => x.UserId == userId).ToList());

        public Task<IReadOnlyList<Role>> GetRolesByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Role>>([]);

        public Task<bool> UserHasRoleAsync(Guid userId, Guid roleId, CancellationToken cancellationToken = default)
            => Task.FromResult(_items.Any(x => x.UserId == userId && x.RoleId == roleId));

        public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(_items.RemoveAll(x => x.Id == id) > 0);

        public Task<bool> DeleteByUserIdAndRoleIdAsync(Guid userId, Guid roleId, CancellationToken cancellationToken = default)
            => Task.FromResult(_items.RemoveAll(x => x.UserId == userId && x.RoleId == roleId) > 0);

        public Task<int> DeleteByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
            => Task.FromResult(_items.RemoveAll(x => x.UserId == userId));
    }

    private sealed class InMemoryTenantFileRepository : ITenantFileRepository
    {
        private readonly List<TenantFile> _items = [];
        public IReadOnlyList<TenantFile> Items => _items;

        public Task<Guid> InsertAsync(TenantFile tenantFile, CancellationToken cancellationToken = default)
        {
            _items.Add(tenantFile);
            return Task.FromResult(tenantFile.Id);
        }

        public Task<TenantFile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(_items.FirstOrDefault(x => x.Id == id));

        public Task<TenantFile?> GetByFileGuidAsync(Guid fileGuid, CancellationToken cancellationToken = default)
            => Task.FromResult(_items.FirstOrDefault(x => x.FileGuid == fileGuid));

        public Task<TenantFile?> GetByTenantAndFileGuidAsync(Guid tenantId, Guid fileGuid, CancellationToken cancellationToken = default)
            => Task.FromResult(_items.FirstOrDefault(x => x.TenantId == tenantId && x.FileGuid == fileGuid));

        public Task<TenantFile?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
            => Task.FromResult(_items.FirstOrDefault(x => x.FileName == name));

        public Task<IReadOnlyList<TenantFile>> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<TenantFile>>(_items.Where(x => x.TenantId == tenantId && x.IsActive).ToList());

        public Task<IReadOnlyList<TenantFile>> GetByStoredFileIdAsync(Guid storedFileId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<TenantFile>>(_items.Where(x => x.StoredFileId == storedFileId && x.IsActive).ToList());

        public Task<bool> SoftDeleteAsync(Guid id, DateTime deletedUtc, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(_items.RemoveAll(x => x.Id == id) > 0);

        public Task<TenantFile?> GetByTenantAndExternalKeyAsync(Guid tenantId, string externalKey, CancellationToken cancellationToken = default)
            => Task.FromResult(_items.FirstOrDefault(x => x.TenantId == tenantId && x.ExternalKey == externalKey));

        public Task<IReadOnlyList<TenantFile>> GetByTenantIdsAsync(IReadOnlyCollection<Guid> tenantIds, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<TenantFile>>(_items.Where(x => tenantIds.Contains(x.TenantId) && x.IsActive).ToList());
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
            => Task.FromResult(_items.FirstOrDefault(x => x.Id == id));

        public Task<StoredFile?> GetBySha256Async(string sha256, CancellationToken cancellationToken = default)
            => Task.FromResult(_items.FirstOrDefault(x => x.Sha256 == sha256));

        public Task<StoredFile?> GetByHashesAsync(string sha256, string crc32, CancellationToken cancellationToken = default)
            => Task.FromResult(_items.FirstOrDefault(x => x.Sha256 == sha256 && x.Crc32 == crc32));

        public Task<IReadOnlyList<StoredFile>> GetActiveAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<StoredFile>>(_items.Where(x => !x.IsDeleted).ToList());

        public Task<bool> IncrementReferenceCountAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task<bool> DecrementReferenceCountAsync(Guid id, CancellationToken cancellationToken = default)
            => DecrementReferenceCountAsync(id, 1, cancellationToken);

        public Task<bool> DecrementReferenceCountAsync(Guid id, int amount, CancellationToken cancellationToken = default)
        {
            var item = _items.FirstOrDefault(x => x.Id == id);
            if (item is null)
                return Task.FromResult(false);

            item.ReferenceCount = Math.Max(0, item.ReferenceCount - amount);
            return Task.FromResult(true);
        }

        public Task<IReadOnlyList<StoredFile>> GetOrphanFilesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<StoredFile>>(_items.Where(x => x.ReferenceCount <= 0 && !x.IsDeleted).ToList());

        public Task<bool> MarkDeletedAsync(Guid id, DateTime deletedUtc, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(_items.RemoveAll(x => x.Id == id) > 0);
    }

    private sealed class InMemoryDeletedTenantRepository : IDeletedTenantRepository
    {
        private readonly List<DeletedTenant> _items = [];
        public IReadOnlyList<DeletedTenant> Items => _items;

        public Task<bool> InsertAsync(DeletedTenant deletedTenant, CancellationToken cancellationToken = default)
        {
            _items.Add(deletedTenant);
            return Task.FromResult(true);
        }

        public Task<IReadOnlyList<DeletedTenant>> GetPendingCleanupAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<DeletedTenant>>(_items.Where(x => x.CleanupCompletedUtc is null).ToList());

        public Task<int> MarkCleanupCompletedAsync(IReadOnlyCollection<Guid> ids, DateTime completedUtc, CancellationToken cancellationToken = default)
            => Task.FromResult(0);
    }

    private sealed class TestPasswordHashService : IPasswordHashService
    {
        public string HashPassword(User user, string password) => $"hash::{password}";
        public bool VerifyPassword(User user, string password) => user.PasswordHash == $"hash::{password}";
    }
}
