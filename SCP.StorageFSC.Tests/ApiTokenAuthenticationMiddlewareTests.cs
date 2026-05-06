using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using System.Security.Claims;
using SCP.StorageFSC.Data.Models;
using SCP.StorageFSC.Data.Repositories;
using SCP.StorageFSC.Security;

namespace SCP.StorageFSC.Tests;

public sealed class ApiTokenAuthenticationMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_WhenNonAdminPrincipalWithoutTenantClaim_DoesNotCreateTenantContext()
    {
        var tenants = new InMemoryTenantRepository();
        var nextCalled = false;
        var sut = CreateMiddleware(_ => nextCalled = true);
        var context = CreateContext(CreatePrincipal(Guid.NewGuid(), tenantId: null, isAdmin: false));

        await sut.InvokeAsync(context, tenants);

        Assert.True(nextCalled);
        Assert.Null(context.Items[TenantContextConstants.CurrentTenantContextItemName]);
    }

    [Fact]
    public async Task InvokeAsync_WhenNonAdminPrincipalWithTenantClaim_CreatesTenantContext()
    {
        var tenant = CreateTenant();
        var tenants = new InMemoryTenantRepository(tenant);
        var nextCalled = false;
        var sut = CreateMiddleware(_ => nextCalled = true);
        var context = CreateContext(CreatePrincipal(Guid.NewGuid(), tenant.Id, isAdmin: false, scopes: ["read", "write", "delete"]));

        await sut.InvokeAsync(context, tenants);

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
        var nextCalled = false;
        var sut = CreateMiddleware(_ => nextCalled = true);
        var context = CreateContext(CreatePrincipal(Guid.NewGuid(), tenantId: null, isAdmin: true, scopes: ["admin"]));

        await sut.InvokeAsync(context, tenants);

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
        var nextCalled = false;
        var sut = CreateMiddleware(_ => nextCalled = true);
        var context = CreateContext(CreatePrincipal(Guid.NewGuid(), tenantId: null, isAdmin: true, scopes: ["admin"]), tenant.ExternalTenantId);

        await sut.InvokeAsync(context, tenants);

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
        var nextCalled = false;
        var sut = CreateMiddleware(_ => nextCalled = true);
        var context = CreateContext(CreatePrincipal(
            Guid.NewGuid(),
            tenantId: null,
            isAdmin: true,
            authType: "web_user",
            scopes: ["admin"]));

        await sut.InvokeAsync(context, tenants);

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
        var nextCalled = false;
        var sut = CreateMiddleware(_ => nextCalled = true);
        var context = CreateContext(CreatePrincipal(
            Guid.NewGuid(),
            tenant.Id,
            isAdmin: false,
            authType: "web_user",
            scopes: ["files.read"]));

        await sut.InvokeAsync(context, tenants);

        Assert.True(nextCalled);

        var current = Assert.IsType<CurrentTenantContext>(
            context.Items[TenantContextConstants.CurrentTenantContextItemName]);
        Assert.Equal(tenant.Id, current.TenantId);
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
        string authType = "api_token",
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

        var identity = new ClaimsIdentity(claims, authType == "web_user" ? "FscCookie" : "ApiKey");
        return new ClaimsPrincipal(identity);
    }

    private static Tenant CreateTenant()
    {
        return new Tenant
        {
            Id = Guid.NewGuid(),
            ExternalTenantId = Guid.NewGuid(),
            Name = "Test tenant",
            IsActive = true,
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
}
