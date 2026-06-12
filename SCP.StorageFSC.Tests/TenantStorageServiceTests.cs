using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using scp.filestorage.Data.Models;
using SCP.StorageFSC.Data.Dto;
using SCP.StorageFSC.Data.Models;
using SCP.StorageFSC.Data.Repositories;
using SCP.StorageFSC.InterfacesService;
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
    private readonly InMemoryUserTwoFactorChallengeRepository _twoFactorChallenges = new();
    private readonly InMemoryUserTwoFactorMethodRepository _twoFactorMethods = new();
    private readonly InMemoryUserRecoveryCodeRepository _recoveryCodes = new();
    private readonly InMemoryTenantFileRepository _tenantFiles = new();
    private readonly InMemoryStoredFileRepository _storedFiles = new();
    private readonly InMemoryDeletedTenantRepository _deletedTenants = new();
    private readonly HttpContextAccessor _httpContextAccessor = new();

    [Fact]
    public async Task GetTenantsAsync_WhenUserIsNotAdmin_ReturnsOnlyCurrentUsersTenants()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var currentUserId = Guid.CreateVersion7();
        await _tenants.InsertAsync(new Tenant
        {
            UserId = currentUserId,
            Name = "Mine",
            ExternalTenantId = Guid.CreateVersion7(),
            IsActive = true
        }, cancellationToken);
        await _tenants.InsertAsync(new Tenant
        {
            UserId = Guid.CreateVersion7(),
            Name = "Other",
            ExternalTenantId = Guid.CreateVersion7(),
            IsActive = true
        }, cancellationToken);

        SetCurrentUser(currentUserId, isAdmin: false);
        var sut = CreateService();

        var result = await sut.GetTenantsAsync(cancellationToken);

        var tenant = Assert.Single(result);
        Assert.Equal("Mine", tenant.Name);
        Assert.Equal(currentUserId, tenant.UserId);
    }

    [Fact]
    public async Task GetTenantTokensAsync_WhenUserIsNotAdmin_ReturnsOnlyCurrentUsersTokens()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var currentUserId = Guid.CreateVersion7();
        var otherUserId = Guid.CreateVersion7();
        var tenant = new Tenant
        {
            UserId = currentUserId,
            Name = "Mine",
            ExternalTenantId = Guid.CreateVersion7(),
            IsActive = true
        };
        await _tenants.InsertAsync(tenant, cancellationToken);

        await _tokens.InsertAsync(new ApiToken
        {
            UserId = currentUserId,
            TenantId = tenant.Id,
            Name = "Current user key",
            TokenHash = "hash1",
            TokenPrefix = "prefix1",
            IsActive = true,
            CanRead = true
        }, cancellationToken);

        await _tokens.InsertAsync(new ApiToken
        {
            UserId = otherUserId,
            TenantId = tenant.Id,
            Name = "Other user key",
            TokenHash = "hash2",
            TokenPrefix = "prefix2",
            IsActive = true,
            CanRead = true
        }, cancellationToken);

        SetCurrentUser(currentUserId, isAdmin: false);
        var sut = CreateService();

        var result = await sut.GetTenantTokensAsync(tenant.Id, cancellationToken);

        var token = Assert.Single(result);
        Assert.Equal("Current user key", token.Name);
        Assert.Equal(currentUserId, token.UserId);
    }

    [Fact]
    public async Task GetTenantFilesAsync_WhenUserOwnsTenant_ReturnsActiveFiles()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var currentUserId = Guid.CreateVersion7();
        var tenant = new Tenant
        {
            UserId = currentUserId,
            Name = "Mine",
            ExternalTenantId = Guid.CreateVersion7(),
            IsActive = true
        };
        await _tenants.InsertAsync(tenant, cancellationToken);

        var activeStoredFile = new StoredFile
        {
            Id = Guid.CreateVersion7(),
            Sha256 = "active-hash",
            Crc32 = "active-crc",
            FileSize = 2048,
            PhysicalPath = "active.bin",
            OriginalFileName = "active.bin",
            ContentType = "application/octet-stream",
            ReferenceCount = 1,
            CreatedUtc = DateTime.UtcNow
        };
        var deletedStoredFile = new StoredFile
        {
            Id = Guid.CreateVersion7(),
            Sha256 = "deleted-hash",
            Crc32 = "deleted-crc",
            FileSize = 4096,
            PhysicalPath = "deleted.bin",
            OriginalFileName = "deleted.bin",
            IsDeleted = true,
            ReferenceCount = 1,
            CreatedUtc = DateTime.UtcNow
        };
        await _storedFiles.InsertAsync(activeStoredFile, cancellationToken);
        await _storedFiles.InsertAsync(deletedStoredFile, cancellationToken);

        await _tenantFiles.InsertAsync(new TenantFile
        {
            TenantId = tenant.Id,
            StoredFileId = activeStoredFile.Id,
            FileGuid = Guid.CreateVersion7(),
            FileName = "active.bin",
            IsActive = true,
            CreatedUtc = DateTime.UtcNow
        }, cancellationToken);
        await _tenantFiles.InsertAsync(new TenantFile
        {
            TenantId = tenant.Id,
            StoredFileId = deletedStoredFile.Id,
            FileGuid = Guid.CreateVersion7(),
            FileName = "deleted.bin",
            IsActive = true,
            CreatedUtc = DateTime.UtcNow
        }, cancellationToken);

        SetCurrentUser(currentUserId, isAdmin: false);
        var sut = CreateService();

        var result = await sut.GetTenantFilesAsync(tenant.Id, cancellationToken);

        var file = Assert.Single(result);
        Assert.Equal("active.bin", file.FileName);
        Assert.Equal(2048, file.FileSize);
        Assert.Equal("active-hash", file.Sha256);
    }

    [Fact]
    public async Task GetTenantFilesAsync_WhenUserDoesNotOwnTenant_ThrowsUnauthorizedAccessException()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenant = new Tenant
        {
            UserId = Guid.CreateVersion7(),
            Name = "Other",
            ExternalTenantId = Guid.CreateVersion7(),
            IsActive = true
        };
        await _tenants.InsertAsync(tenant, cancellationToken);

        SetCurrentUser(Guid.CreateVersion7(), isAdmin: false);
        var sut = CreateService();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            sut.GetTenantFilesAsync(tenant.Id, cancellationToken));
    }

    [Fact]
    public async Task GetUsersWithTenantsAsync_WhenUserIsAdmin_ReturnsUsersWithTheirTenants()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
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
        await _users.InsertAsync(firstUser, cancellationToken);
        await _users.InsertAsync(secondUser, cancellationToken);

        await _tenants.InsertAsync(new Tenant
        {
            UserId = firstUser.Id,
            Name = "Alice tenant",
            ExternalTenantId = Guid.CreateVersion7(),
            IsActive = true
        }, cancellationToken);

        SetCurrentUser(Guid.CreateVersion7(), isAdmin: true);
        var sut = CreateUserService();

        var result = await sut.GetUsersWithTenantsAsync(cancellationToken);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, item => item.UserName == "Alice" && item.Tenants.Count == 1);
        Assert.Contains(result, item => item.UserName == "Bob" && item.Tenants.Count == 0);
    }

    [Fact]
    public async Task UpdateTenantAsync_ChangesNameAndStatus()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenant = new Tenant
        {
            UserId = Guid.CreateVersion7(),
            Name = "Alpha",
            ExternalTenantId = Guid.CreateVersion7(),
            IsActive = true
        };
        await _tenants.InsertAsync(tenant, cancellationToken);

        SetCurrentUser(Guid.CreateVersion7(), isAdmin: true);
        var sut = CreateService();

        var result = await sut.UpdateTenantAsync(tenant.Id, new UpdateTenantRequest
        {
            Name = "Beta",
            IsActive = false
        }, cancellationToken);

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
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenant = new Tenant
        {
            UserId = Guid.CreateVersion7(),
            Name = "Delete me",
            ExternalTenantId = Guid.CreateVersion7(),
            IsActive = true
        };
        await _tenants.InsertAsync(tenant, cancellationToken);

        SetCurrentUser(Guid.CreateVersion7(), isAdmin: true);
        var sut = CreateService();

        var deleted = await sut.DeleteTenantAsync(tenant.Id, cancellationToken);

        Assert.True(deleted);
        Assert.Null(await _tenants.GetByIdAsync(tenant.Id, cancellationToken));
    }

    [Fact]
    public async Task CreateTenantAsync_AssignsTenantToRequestedUser()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var owner = new User
        {
            Id = Guid.CreateVersion7(),
            Name = "Owner",
            NormalizedName = "OWNER",
            PasswordHash = "hash"
        };
        await _users.InsertAsync(owner, cancellationToken);
        SetCurrentUser(Guid.CreateVersion7(), isAdmin: true);

        var sut = CreateService();

        var result = await sut.CreateTenantAsync(new CreateTenantRequest
        {
            UserId = owner.Id,
            Name = "Tenant A"
        }, cancellationToken);

        Assert.Equal(owner.Id, result.UserId);
        Assert.Equal(owner.Id, (await _tenants.GetByIdAsync(result.Id, cancellationToken))!.UserId);
    }

    [Fact]
    public async Task CreateTenantApiTokenAsync_UsesTenantOwner()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var owner = new User
        {
            Id = Guid.CreateVersion7(),
            Name = "Owner",
            NormalizedName = "OWNER",
            PasswordHash = "hash"
        };
        await _users.InsertAsync(owner, cancellationToken);
        var tenant = new Tenant
        {
            UserId = owner.Id,
            Name = "Tenant A",
            ExternalTenantId = Guid.CreateVersion7(),
            IsActive = true
        };
        await _tenants.InsertAsync(tenant, cancellationToken);
        SetCurrentUser(Guid.CreateVersion7(), isAdmin: true);
        var sut = CreateService();

        var result = await sut.CreateTenantApiTokenAsync(tenant.Id, new CreateTenantApiTokenRequest
        {
            Name = "Tenant key",
            CanRead = true,
            CanWrite = true
        }, cancellationToken);

        Assert.NotNull(result);
        Assert.Equal(owner.Id, result.Token.UserId);
        Assert.Equal(tenant.Id, result.Token.TenantId);
        Assert.NotEmpty(result.PlainTextToken);
    }

    [Fact]
    public async Task DeleteTenantApiTokenAsync_WhenTokenBelongsToAnotherTenant_ReturnsFalse()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenant = new Tenant
        {
            UserId = Guid.CreateVersion7(),
            Name = "Tenant A",
            ExternalTenantId = Guid.CreateVersion7(),
            IsActive = true
        };
        var otherTenant = new Tenant
        {
            UserId = tenant.UserId,
            Name = "Tenant B",
            ExternalTenantId = Guid.CreateVersion7(),
            IsActive = true
        };
        await _tenants.InsertAsync(tenant, cancellationToken);
        await _tenants.InsertAsync(otherTenant, cancellationToken);
        var token = new ApiToken
        {
            UserId = tenant.UserId,
            TenantId = otherTenant.Id,
            Name = "Other key",
            TokenHash = "hash",
            TokenPrefix = "prefix",
            IsActive = true
        };
        await _tokens.InsertAsync(token, cancellationToken);
        SetCurrentUser(Guid.CreateVersion7(), isAdmin: true);
        var sut = CreateService();

        var deleted = await sut.DeleteTenantApiTokenAsync(tenant.Id, token.Id, cancellationToken);

        Assert.False(deleted);
        Assert.NotNull(await _tokens.GetByIdAsync(token.Id, cancellationToken));
    }

    [Fact]
    public async Task DeleteUserAsync_RemovesOwnedTenantsAndQueuesDeletedTenantRecords()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var owner = new User
        {
            Id = Guid.CreateVersion7(),
            Name = "Owner",
            NormalizedName = "OWNER",
            PasswordHash = "hash"
        };
        await _users.InsertAsync(owner, cancellationToken);
        await _userRoles.InsertAsync(new UserRole
        {
            UserId = Guid.CreateVersion7(),
            RoleId = scp.filestorage.Data.Models.SystemRoles.AdministratorId,
            CreatedUtc = DateTime.UtcNow
        }, cancellationToken);

        var tenant = new Tenant
        {
            UserId = owner.Id,
            Name = "Owned tenant",
            ExternalTenantId = Guid.CreateVersion7(),
            IsActive = true
        };
        await _tenants.InsertAsync(tenant, cancellationToken);
        await _tenantFiles.InsertAsync(new TenantFile
        {
            TenantId = tenant.Id,
            StoredFileId = Guid.CreateVersion7(),
            FileGuid = Guid.CreateVersion7(),
            FileName = "file.bin",
            IsActive = true,
            CreatedUtc = DateTime.UtcNow
        }, cancellationToken);
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
        }, cancellationToken);

        SetCurrentUser(Guid.CreateVersion7(), isAdmin: true);
        var sut = CreateUserService();

        var deleted = await sut.DeleteUserAsync(owner.Id, cancellationToken);

        Assert.True(deleted);
        Assert.Null(await _users.GetByIdAsync(owner.Id, cancellationToken));
        Assert.Null(await _tenants.GetByIdAsync(tenant.Id, cancellationToken));
        Assert.Single(_deletedTenants.Items);
        Assert.Equal(0, _storedFiles.Items[0].ReferenceCount);
    }

    [Fact]
    public async Task UpdateApiTokenAsync_ChangesPermissionsAndActivation()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenant = new Tenant
        {
            UserId = Guid.CreateVersion7(),
            Name = "Tenant",
            ExternalTenantId = Guid.CreateVersion7(),
            IsActive = true
        };
        await _tenants.InsertAsync(tenant, cancellationToken);

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
        await _tokens.InsertAsync(token, cancellationToken);

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
        }, cancellationToken);

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
        var cancellationToken = TestContext.Current.CancellationToken;
        SetCurrentUser(Guid.CreateVersion7(), isAdmin: false);
        var sut = CreateService();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            sut.CreateTenantAsync(new CreateTenantRequest
            {
                Name = "Forbidden"
            }, cancellationToken));
    }

    [Fact]
    public async Task CreateUserAsync_UsesTemporaryPasswordAndRequestedFlags()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        SetCurrentUser(Guid.CreateVersion7(), isAdmin: true);
        var sut = CreateUserService();

        var result = await sut.CreateUserAsync(new CreateUserRequest
        {
            Name = "Alice",
            Email = "alice@example.com",
            PhoneNumber = "+123456789",
            TemporaryPassword = "TemporaryPassword123!",
            IsActive = false,
            MustChangePassword = true
        }, cancellationToken);

        var user = await _users.GetByIdAsync(result.UserId, cancellationToken);
        Assert.NotNull(user);
        Assert.Equal("hash::TemporaryPassword123!", user.PasswordHash);
        Assert.Equal("+123456789", user.PhoneNumber);
        Assert.False(user.IsActive);
        Assert.True(user.MustChangePassword);
        Assert.NotNull(user.PasswordChangedUtc);
    }

    [Fact]
    public async Task UpdateUserProfileAsync_DoesNotChangeSecurityStateOrAuditFields()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var user = new User
        {
            Name = "Bob",
            NormalizedName = "BOB",
            Email = "bob@example.com",
            NormalizedEmail = "BOB@EXAMPLE.COM",
            PhoneNumber = "+111",
            PhoneNumberConfirmed = true,
            FailedLoginCount = 3,
            LastFailedLoginUtc = DateTime.UtcNow.AddDays(-2),
            LastLoginUtc = DateTime.UtcNow.AddDays(-1),
            LastLoginIpAddress = "192.0.2.10",
            TwoFactorEnabled = true,
            TwoFactorLastUsedUtc = DateTime.UtcNow.AddHours(-3),
            PasswordChangedUtc = DateTime.UtcNow.AddDays(-7),
            PasswordExpiresUtc = DateTime.UtcNow.AddDays(7),
            SecurityStamp = "original-stamp"
        };
        await _users.InsertAsync(user, cancellationToken);
        SetCurrentUser(Guid.CreateVersion7(), isAdmin: true);
        var sut = CreateUserService();

        var result = await sut.UpdateUserProfileAsync(user.Id, new UpdateUserProfileRequest
        {
            Name = "Bob Updated",
            Email = "bob@example.com",
            PhoneNumber = "+222",
            ExternalUserId = "external-1",
            Comment = "note"
        }, cancellationToken);

        Assert.NotNull(result);
        Assert.Equal(3, user.FailedLoginCount);
        Assert.NotNull(user.LastFailedLoginUtc);
        Assert.NotNull(user.LastLoginUtc);
        Assert.True(user.TwoFactorEnabled);
        Assert.NotNull(user.TwoFactorLastUsedUtc);
        Assert.NotNull(user.PasswordChangedUtc);
        Assert.NotNull(user.PasswordExpiresUtc);
        Assert.Equal("original-stamp", user.SecurityStamp);
        Assert.Equal("external-1", user.ExternalUserId);
        Assert.Equal("note", user.Comment);
    }

    [Fact]
    public async Task ResetUserPasswordAsync_UpdatesPasswordSecurityState()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var user = new User
        {
            Name = "Carol",
            NormalizedName = "CAROL",
            PasswordHash = "old-hash",
            SecurityStamp = "old-stamp",
            FailedLoginCount = 5,
            LastFailedLoginUtc = DateTime.UtcNow.AddMinutes(-5)
        };
        await _users.InsertAsync(user, cancellationToken);
        SetCurrentUser(Guid.CreateVersion7(), isAdmin: true);
        var sut = CreateUserService();

        var changed = await sut.ResetUserPasswordAsync(user.Id, new ResetUserPasswordRequest
        {
            NewPassword = "TemporaryPassword123!",
            MustChangePassword = true
        }, cancellationToken);

        Assert.True(changed);
        Assert.Equal("hash::TemporaryPassword123!", user.PasswordHash);
        Assert.NotEqual("old-stamp", user.SecurityStamp);
        Assert.Equal(0, user.FailedLoginCount);
        Assert.Null(user.LastFailedLoginUtc);
        Assert.True(user.MustChangePassword);
        Assert.NotNull(user.PasswordChangedUtc);
    }

    [Fact]
    public async Task EnableUserTwoFactorAsync_WhenNoActiveMethod_ThrowsInvalidOperationException()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var user = new User
        {
            Name = "Diana",
            NormalizedName = "DIANA",
            PasswordHash = "hash"
        };
        await _users.InsertAsync(user, cancellationToken);
        SetCurrentUser(Guid.CreateVersion7(), isAdmin: true);
        var sut = CreateUserService();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.EnableUserTwoFactorAsync(user.Id, cancellationToken));

        Assert.False(user.TwoFactorEnabled);
        Assert.Null(user.TwoFactorEnabledUtc);
    }

    [Fact]
    public async Task EnableUserTwoFactorAsync_WhenActiveMethodExists_EnablesTwoFactor()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var user = new User
        {
            Name = "Erin",
            NormalizedName = "ERIN",
            PasswordHash = "hash"
        };
        await _users.InsertAsync(user, cancellationToken);
        await _twoFactorMethods.InsertAsync(new UserTwoFactorMethod
        {
            UserId = user.Id,
            MethodType = TwoFactorMethodType.AuthenticatorApp,
            IsEnabled = true,
            IsConfirmed = true,
            IsDefault = true,
            ConfirmedUtc = DateTime.UtcNow
        }, cancellationToken);
        SetCurrentUser(Guid.CreateVersion7(), isAdmin: true);
        var sut = CreateUserService();

        var enabled = await sut.EnableUserTwoFactorAsync(user.Id, cancellationToken);

        Assert.True(enabled);
        Assert.True(user.TwoFactorEnabled);
        Assert.NotNull(user.TwoFactorEnabledUtc);
    }

    [Fact]
    public async Task ConfirmUserEmailAsync_WhenCodeIsValid_ConfirmsEmailAndCreatesEmailTwoFactorMethod()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var user = new User
        {
            Name = "Frank",
            NormalizedName = "FRANK",
            Email = "frank@example.com",
            NormalizedEmail = "FRANK@EXAMPLE.COM",
            EmailConfirmed = false,
            PasswordHash = "hash"
        };
        await _users.InsertAsync(user, cancellationToken);
        SetCurrentUser(Guid.CreateVersion7(), isAdmin: true);
        var sender = new TestOneTimeCodeSender();
        var sut = CreateUserService(sender);

        var sent = await sut.SendUserEmailConfirmationAsync(user.Id, cancellationToken);
        var confirmed = await sut.ConfirmUserEmailAsync(user.Id, sender.LastEmailCode!, cancellationToken);

        var emailMethod = await _twoFactorMethods.GetByUserAndTypeAsync(
            user.Id,
            TwoFactorMethodType.Email,
            cancellationToken);

        Assert.True(sent);
        Assert.True(confirmed);
        Assert.True(user.EmailConfirmed);
        Assert.NotNull(emailMethod);
        Assert.True(emailMethod!.IsEnabled);
        Assert.True(emailMethod.IsConfirmed);
        Assert.Equal("frank@example.com", emailMethod.Destination);
        Assert.Equal("fr***@example.com", emailMethod.MaskedDestination);
    }

    private TenantStorageService CreateService()
    {
        return new TenantStorageService(
            _tenants,
            _tokens,
            _users,
            _tenantFiles,
            _storedFiles,
            _deletedTenants,
            _httpContextAccessor,
            NullLogger<TenantStorageService>.Instance);
    }

    private UserStorageService CreateUserService(TestOneTimeCodeSender? oneTimeCodeSender = null)
    {
        return new UserStorageService(
            _tenants,
            _tokens,
            _users,
            _userRoles,
            _twoFactorChallenges,
            _tenantFiles,
            _storedFiles,
            _deletedTenants,
            new TestPasswordHashService(),
            _twoFactorMethods,
            _recoveryCodes,
            new TestAuthenticationHashService(),
            new FakeUserAuthenticationAuditService(),
            oneTimeCodeSender ?? new TestOneTimeCodeSender(),
            _httpContextAccessor,
            NullLogger<UserStorageService>.Instance);
    }

        private sealed class FakeUserAuthenticationAuditService : IUserAuthenticationAuditService
        {
            public Task LogPasswordLoginAsync(HttpContext context, string login, LoginResult result, CancellationToken cancellationToken = default)
                => Task.CompletedTask;

            public Task LogTwoFactorAsync(HttpContext context, VerifyTwoFactorResult result, string eventType, CancellationToken cancellationToken = default)
                => Task.CompletedTask;

            public Task<UserAuthenticationNotificationPageDto> GetNotificationsAsync(Guid? userId = null, int pageNumber = 1, int pageSize = 20, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(new UserAuthenticationNotificationPageDto
                {
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    TotalCount = 0,
                    Items = []
                });
            }
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

        public Task<bool> RecalculateTotalSizeBytesAsync(Guid tenantId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_items.Any(item => item.Id == tenantId));
        }

        public Task<int> RecalculateAllTotalSizeBytesAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_items.Count);
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

        public Task<bool> UpdateAsync(TenantFile tenantFile, CancellationToken cancellationToken = default)
        {
            var index = _items.FindIndex(x => x.Id == tenantFile.Id);
            if (index < 0)
                return Task.FromResult(false);

            _items[index] = tenantFile;
            return Task.FromResult(true);
        }

        public Task<TenantFile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(_items.FirstOrDefault(x => x.Id == id));

        public Task<TenantFile?> GetByFileGuidAsync(Guid fileGuid, CancellationToken cancellationToken = default)
            => Task.FromResult(_items.FirstOrDefault(x => x.FileGuid == fileGuid));

        public Task<TenantFile?> GetByExternalKeyAsync(Guid tenantId, string externalKey, CancellationToken cancellationToken = default)
            => Task.FromResult(_items.FirstOrDefault(x => x.TenantId == tenantId && x.ExternalKey == externalKey && x.IsActive));

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

    private sealed class TestAuthenticationHashService : IAuthenticationHashService
    {
        public string HashSecret(string value) => $"hash::{value}";
    }

    private sealed class TestOneTimeCodeSender : IOneTimeCodeSender
    {
        public string? LastEmail { get; private set; }
        public string? LastEmailCode { get; private set; }

        public Task SendEmailCodeAsync(string email, string code, CancellationToken cancellationToken = default)
        {
            LastEmail = email;
            LastEmailCode = code;
            return Task.CompletedTask;
        }

        public Task SendSmsCodeAsync(string phoneNumber, string code, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class InMemoryUserTwoFactorChallengeRepository : IUserTwoFactorChallengeRepository
    {
        private readonly List<UserTwoFactorChallenge> _items = [];

        public Task<bool> InsertAsync(UserTwoFactorChallenge challenge, CancellationToken cancellationToken = default)
        {
            _items.Add(challenge);
            return Task.FromResult(true);
        }

        public Task<UserTwoFactorChallenge?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(_items.FirstOrDefault(x => x.Id == id));

        public Task<IReadOnlyList<UserTwoFactorChallenge>> GetPendingByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<UserTwoFactorChallenge>>(_items
                .Where(x => x.UserId == userId && x.VerifiedUtc is null && x.ExpiresUtc > DateTime.UtcNow)
                .ToList());

        public Task<bool> UpdateAsync(UserTwoFactorChallenge challenge, CancellationToken cancellationToken = default)
            => Task.FromResult(_items.Any(x => x.Id == challenge.Id));

        public Task<bool> DeleteExpiredAsync(DateTime utcNow, CancellationToken cancellationToken = default)
            => Task.FromResult(_items.RemoveAll(x => x.ExpiresUtc <= utcNow) > 0);
    }

    private sealed class InMemoryUserTwoFactorMethodRepository : IUserTwoFactorMethodRepository
    {
        private readonly List<UserTwoFactorMethod> _items = [];

        public Task<bool> InsertAsync(UserTwoFactorMethod method, CancellationToken cancellationToken = default)
        {
            _items.Add(method);
            return Task.FromResult(true);
        }

        public Task<UserTwoFactorMethod?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(_items.FirstOrDefault(x => x.Id == id));

        public Task<IReadOnlyList<UserTwoFactorMethod>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<UserTwoFactorMethod>>(_items.Where(x => x.UserId == userId).ToList());

        public Task<UserTwoFactorMethod?> GetDefaultAsync(Guid userId, CancellationToken cancellationToken = default)
            => Task.FromResult(_items.FirstOrDefault(x => x.UserId == userId && x.IsDefault));

        public Task<UserTwoFactorMethod?> GetByUserAndTypeAsync(Guid userId, TwoFactorMethodType methodType, CancellationToken cancellationToken = default)
            => Task.FromResult(_items.FirstOrDefault(x => x.UserId == userId && x.MethodType == methodType));

        public Task<bool> UpdateAsync(UserTwoFactorMethod method, CancellationToken cancellationToken = default)
            => Task.FromResult(_items.Any(x => x.Id == method.Id));

        public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(_items.RemoveAll(x => x.Id == id) > 0);
    }

    private sealed class InMemoryUserRecoveryCodeRepository : IUserRecoveryCodeRepository
    {
        private readonly List<UserRecoveryCode> _items = [];

        public Task<bool> InsertAsync(UserRecoveryCode code, CancellationToken cancellationToken = default)
        {
            _items.Add(code);
            return Task.FromResult(true);
        }

        public Task<int> InsertManyAsync(IEnumerable<UserRecoveryCode> codes, CancellationToken cancellationToken = default)
        {
            var list = codes.ToList();
            _items.AddRange(list);
            return Task.FromResult(list.Count);
        }

        public Task<IReadOnlyList<UserRecoveryCode>> GetUnusedByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<UserRecoveryCode>>(_items.Where(x => x.UserId == userId && !x.IsUsed).ToList());

        public Task<UserRecoveryCode?> GetUnusedByHashAsync(Guid userId, string codeHash, CancellationToken cancellationToken = default)
            => Task.FromResult(_items.FirstOrDefault(x => x.UserId == userId && x.CodeHash == codeHash && !x.IsUsed));

        public Task<bool> MarkUsedAsync(Guid id, DateTime usedUtc, string? ipAddress, string? userAgent, CancellationToken cancellationToken = default)
        {
            var code = _items.FirstOrDefault(x => x.Id == id);
            if (code is null)
                return Task.FromResult(false);

            code.IsUsed = true;
            code.UsedUtc = usedUtc;
            code.UsedIpAddress = ipAddress;
            code.MarkUpdated();
            return Task.FromResult(true);
        }

        public Task<int> DeleteByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
            => Task.FromResult(_items.RemoveAll(x => x.UserId == userId));
    }
}
