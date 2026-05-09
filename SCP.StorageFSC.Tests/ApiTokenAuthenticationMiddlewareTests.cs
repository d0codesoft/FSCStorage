using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using System.Security.Claims;
using SCP.StorageFSC.Data.Models;
using SCP.StorageFSC.Data.Repositories;
using SCP.StorageFSC.Security;
using scp.filestorage.Security;

namespace SCP.StorageFSC.Tests;

public sealed class ApiTokenAuthenticationMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_WhenNonAdminPrincipalWithoutTenantClaim_DoesNotCreateTenantContext()
    {
        var tenants = new InMemoryTenantRepository();
        var tokens = new InMemoryApiTokenRepository();
        var nextCalled = false;
        var sut = CreateMiddleware(_ => nextCalled = true);
        var context = CreateContext(CreatePrincipal(Guid.NewGuid(), tenantId: null, isAdmin: false));

        await sut.InvokeAsync(context, tenants, tokens);

        Assert.True(nextCalled);
        Assert.Null(context.Items[TenantContextConstants.CurrentTenantContextItemName]);
    }

    [Fact]
    public async Task InvokeAsync_WhenNonAdminPrincipalWithTenantClaim_CreatesTenantContext()
    {
        var tenant = CreateTenant();
        var tenants = new InMemoryTenantRepository(tenant);
        var tokens = new InMemoryApiTokenRepository();
        var nextCalled = false;
        var sut = CreateMiddleware(_ => nextCalled = true);
        var context = CreateContext(CreatePrincipal(Guid.NewGuid(), tenant.Id, isAdmin: false, scopes: ["read", "write", "delete"]));

        await sut.InvokeAsync(context, tenants, tokens);

        Assert.True(nextCalled);

        var current = Assert.IsType<CurrentTenantContext>(
            context.Items[TenantContextConstants.CurrentTenantContextItemName]);
        Assert.Equal(tenant.Id, current.TenantId);
        Assert.Equal(tenant.ExternalTenantId, current.TenantGuid);
        Assert.False(current.IsAdmin);
        Assert.True(current.CanRead);
        Assert.True(current.CanWrite);
        Assert.True(current.CanDelete);
    }

    [Fact]
    public async Task InvokeAsync_WhenAdminPrincipalWithoutTenantHeader_CreatesAdminContextWithoutTenant()
    {
        var tenants = new InMemoryTenantRepository();
        var tokens = new InMemoryApiTokenRepository();
        var nextCalled = false;
        var sut = CreateMiddleware(_ => nextCalled = true);
        var context = CreateContext(CreatePrincipal(Guid.NewGuid(), tenantId: null, isAdmin: true, scopes: ["admin"]));

        await sut.InvokeAsync(context, tenants, tokens);

        Assert.True(nextCalled);

        var current = Assert.IsType<CurrentTenantContext>(
            context.Items[TenantContextConstants.CurrentTenantContextItemName]);
        Assert.Null(current.TenantId);
        Assert.Null(current.TenantGuid);
        Assert.True(current.IsAdmin);
    }

    [Fact]
    public async Task InvokeAsync_WhenAdminPrincipalWithTenantHeader_CreatesContextForRequestedTenant()
    {
        var tenant = CreateTenant();
        var tenants = new InMemoryTenantRepository(tenant);
        var tokens = new InMemoryApiTokenRepository();
        var nextCalled = false;
        var sut = CreateMiddleware(_ => nextCalled = true);
        var context = CreateContext(CreatePrincipal(Guid.NewGuid(), tenantId: null, isAdmin: true, scopes: ["admin"]), tenant.ExternalTenantId);

        await sut.InvokeAsync(context, tenants, tokens);

        Assert.True(nextCalled);

        var current = Assert.IsType<CurrentTenantContext>(
            context.Items[TenantContextConstants.CurrentTenantContextItemName]);
        Assert.Equal(tenant.Id, current.TenantId);
        Assert.Equal(tenant.ExternalTenantId, current.TenantGuid);
        Assert.True(current.IsAdmin);
    }

    [Fact]
    public async Task InvokeAsync_WhenWebUserPrincipalWithAdminRole_CreatesAdminContext()
    {
        var tenants = new InMemoryTenantRepository();
        var tokens = new InMemoryApiTokenRepository();
        var nextCalled = false;
        var sut = CreateMiddleware(_ => nextCalled = true);
        var context = CreateContext(CreatePrincipal(
            Guid.NewGuid(),
            tenantId: null,
            isAdmin: true,
            authType: AuthType.WebApp,
            scopes: ["admin"]));

        await sut.InvokeAsync(context, tenants, tokens);

        Assert.True(nextCalled);

        var current = Assert.IsType<CurrentTenantContext>(
            context.Items[TenantContextConstants.CurrentTenantContextItemName]);
        Assert.Null(current.TenantId);
        Assert.True(current.IsAdmin);
    }

    [Fact]
    public async Task InvokeAsync_WhenWebUserPrincipalWithTenantClaim_CreatesTenantContext()
    {
        var tenant = CreateTenant();
        var tenants = new InMemoryTenantRepository(tenant);
        var tokens = new InMemoryApiTokenRepository();
        var nextCalled = false;
        var sut = CreateMiddleware(_ => nextCalled = true);
        var context = CreateContext(CreatePrincipal(
            Guid.NewGuid(),
            tenant.Id,
            isAdmin: false,
            authType: AuthType.WebApp,
            scopes: ["files.read"]));

        await sut.InvokeAsync(context, tenants, tokens);

        Assert.True(nextCalled);

        var current = Assert.IsType<CurrentTenantContext>(
            context.Items[TenantContextConstants.CurrentTenantContextItemName]);
        Assert.Equal(tenant.Id, current.TenantId);
        Assert.False(current.IsAdmin);
        Assert.True(current.CanRead);
    }

    [Fact]
    public async Task InvokeAsync_WhenWebUserPrincipalWithoutTenantClaim_LoadsFirstTokenAndTenantByUserId()
    {
        var userId = Guid.NewGuid();
        var tenant = CreateTenant(userId);
        var token = CreateToken(userId, tenant.Id);
        var tenants = new InMemoryTenantRepository(tenant);
        var tokens = new InMemoryApiTokenRepository(token);
        var nextCalled = false;
        var sut = CreateMiddleware(_ => nextCalled = true);
        var context = CreateContext(CreatePrincipal(
            userId,
            tenantId: null,
            isAdmin: false,
            authType: AuthType.WebApp,
            scopes: ["files.read"]));

        await sut.InvokeAsync(context, tenants, tokens);

        Assert.True(nextCalled);

        var current = Assert.IsType<CurrentTenantContext>(
            context.Items[TenantContextConstants.CurrentTenantContextItemName]);
        Assert.Equal(token.Id, current.TokenId);
        Assert.Equal(tenant.Id, current.TenantId);
        Assert.Equal(tenant.ExternalTenantId, current.TenantGuid);
        Assert.False(current.IsAdmin);
        Assert.True(current.CanRead);
    }

    private static ApiTokenAuthenticationMiddleware CreateMiddleware(Action<HttpContext> onNext)
    {
        return new ApiTokenAuthenticationMiddleware(
            context =>
            {
                onNext(context);
                return Task.CompletedTask;
            },
            NullLogger<ApiTokenAuthenticationMiddleware>.Instance);
    }

    private static DefaultHttpContext CreateContext(ClaimsPrincipal user, Guid? tenantGuid = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/file";
        context.User = user;

        if (tenantGuid.HasValue)
            context.Request.Headers["X-Tenant-Id"] = tenantGuid.Value.ToString();

        return context;
    }

    private static ClaimsPrincipal CreatePrincipal(
        Guid tokenId,
        Guid? tenantId,
        bool isAdmin,
        string authType = AuthType.ApiToken,
        params string[] scopes)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, tokenId.ToString()),
            new(ClaimTypes.Name, isAdmin ? "Admin" : "Tenant"),
            new("auth_type", authType)
        };

        if (tenantId.HasValue)
            claims.Add(new Claim("tenant_id", tenantId.Value.ToString()));

        if (isAdmin)
            claims.Add(new Claim(ClaimTypes.Role, "Admin"));

        foreach (var scope in scopes)
            claims.Add(new Claim("scope", scope));

        var identity = new ClaimsIdentity(claims, authType == AuthType.WebApp ? "FscCookie" : "ApiKey");
        return new ClaimsPrincipal(identity);
    }

    private static Tenant CreateTenant(Guid? userId = null)
    {
        return new Tenant
        {
            Id = Guid.NewGuid(),
            UserId = userId ?? Guid.NewGuid(),
            ExternalTenantId = Guid.NewGuid(),
            Name = "Test tenant",
            IsActive = true,
            CreatedUtc = DateTime.UtcNow
        };
    }

    private static ApiToken CreateToken(Guid userId, Guid? tenantId)
    {
        return new ApiToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TenantId = tenantId,
            Name = "Test token",
            TokenHash = "hash",
            TokenPrefix = "prefix",
            IsActive = true,
            CanRead = true,
            CreatedUtc = DateTime.UtcNow
        };
    }

    private sealed class InMemoryTenantRepository : ITenantRepository
    {
        private readonly List<Tenant> _tenants;

        public InMemoryTenantRepository(params Tenant[] tenants)
        {
            _tenants = tenants.ToList();
        }

        public Task<bool> InsertAsync(Tenant tenant, CancellationToken cancellationToken = default)
        {
            _tenants.Add(tenant);
            return Task.FromResult(true);
        }

        public Task<Tenant?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_tenants.FirstOrDefault(tenant => tenant.Id == id));
        }

        public Task<Tenant?> GetByGuidAsync(Guid tenantGuid, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_tenants.FirstOrDefault(tenant => tenant.ExternalTenantId == tenantGuid));
        }

        public Task<Tenant?> GetFirstByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_tenants.OrderBy(tenant => tenant.CreatedUtc).ThenBy(tenant => tenant.Id).FirstOrDefault(tenant => tenant.UserId == userId));
        }

        public Task<Tenant?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_tenants.FirstOrDefault(tenant => tenant.Name == name));
        }

        public Task<IReadOnlyList<Tenant>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<Tenant>>(_tenants.ToList());
        }

        public Task<bool> UpdateAsync(Tenant tenant, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_tenants.Any(item => item.Id == tenant.Id));
        }

        public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_tenants.RemoveAll(tenant => tenant.Id == id) > 0);
        }
    }

    private sealed class InMemoryApiTokenRepository : IApiTokenRepository
    {
        private readonly List<ApiToken> _tokens;

        public InMemoryApiTokenRepository(params ApiToken[] tokens)
        {
            _tokens = tokens.ToList();
        }

        public Task<Guid> InsertAsync(ApiToken token, CancellationToken cancellationToken = default)
        {
            _tokens.Add(token);
            return Task.FromResult(token.Id);
        }

        public Task<ApiToken?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_tokens.FirstOrDefault(token => token.Id == id));
        }

        public Task<ApiToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_tokens.FirstOrDefault(token => token.TokenHash == tokenHash));
        }

        public Task<ApiToken?> GetFirstByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_tokens.OrderBy(token => token.CreatedUtc).ThenBy(token => token.Id).FirstOrDefault(token => token.UserId == userId));
        }

        public Task<IReadOnlyList<ApiToken>> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<ApiToken>>(_tokens.Where(token => token.TenantId == tenantId).ToList());
        }

        public Task<bool> UpdateLastUsedAsync(Guid id, DateTime lastUsedUtc, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_tokens.Any(token => token.Id == id));
        }

        public Task<bool> UpdateLastUsedAsync(ApiToken token, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_tokens.Any(item => item.Id == token.Id));
        }

        public Task<bool> RevokeAsync(Guid id, DateTime revokedUtc, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_tokens.Any(token => token.Id == id));
        }

        public Task<bool> RevokeAsync(ApiToken token, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_tokens.Any(item => item.Id == token.Id));
        }

        public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_tokens.RemoveAll(token => token.Id == id) > 0);
        }

        public Task<bool> HasAnyAdminTokenAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_tokens.Any(token => token.IsAdmin));
        }

        public Task<ApiToken?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_tokens.FirstOrDefault(token => token.Name == name));
        }
    }
}
