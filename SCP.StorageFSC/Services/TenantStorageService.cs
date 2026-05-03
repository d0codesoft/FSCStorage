using SCP.StorageFSC.Data.Dto;
using SCP.StorageFSC.Data.Models;
using SCP.StorageFSC.Data.Repositories;
using SCP.StorageFSC.InterfacesService;
using SCP.StorageFSC.Security;

namespace SCP.StorageFSC.Services
{
    public sealed class TenantStorageService : ITenantStorageService
    {
        private readonly ITenantRepository _tenantRepository;
        private readonly IApiTokenRepository _apiTokenRepository;
        private readonly ICurrentTenantAccessor _currentTenantAccessor;
        private readonly ILogger<TenantStorageService> _logger;

        public TenantStorageService(
            ITenantRepository tenantRepository,
            IApiTokenRepository apiTokenRepository,
            ICurrentTenantAccessor currentTenantAccessor,
            ILogger<TenantStorageService> logger)
        {
            _tenantRepository = tenantRepository;
            _apiTokenRepository = apiTokenRepository;
            _currentTenantAccessor = currentTenantAccessor;
            _logger = logger;
        }

        public async Task<TenantDto> CreateTenantAsync(
            CreateTenantRequest request,
            CancellationToken cancellationToken = default)
        {
            EnsureAdmin();

            if (string.IsNullOrWhiteSpace(request.Name))
                throw new ArgumentException("Tenant name is required.", nameof(request));

            var existing = await _tenantRepository.GetByNameAsync(request.Name.Trim(), cancellationToken);
            if (existing is not null)
                throw new InvalidOperationException($"Tenant with name '{request.Name}' already exists.");

            var entity = new Tenant
            {
                ExternalTenantId = Guid.NewGuid(),
                Name = request.Name.Trim(),
                IsActive = true,
                CreatedUtc = DateTime.UtcNow
            };

            _ = await _tenantRepository.InsertAsync(entity, cancellationToken);

            _logger.LogInformation(
                "Tenant created. TenantId={TenantId}, TenantGuid={TenantGuid}, Name={TenantName}",
                entity.Id,
                entity.ExternalTenantId,
                entity.Name);

            return MapTenant(entity);
        }

        public async Task<TenantDto?> GetTenantByIdAsync(
            Guid tenantId,
            CancellationToken cancellationToken = default)
        {
            EnsureAdmin();

            var tenant = await _tenantRepository.GetByIdAsync(tenantId, cancellationToken);
            return tenant is null ? null : MapTenant(tenant);
        }

        public async Task<TenantDto?> GetTenantByGuidAsync(
            Guid tenantGuid,
            CancellationToken cancellationToken = default)
        {
            EnsureAdmin();

            var tenant = await _tenantRepository.GetByGuidAsync(tenantGuid, cancellationToken);
            return tenant is null ? null : MapTenant(tenant);
        }

        public async Task<IReadOnlyList<TenantDto>> GetTenantsAsync(
            CancellationToken cancellationToken = default)
        {
            EnsureAdmin();

            var tenants = await _tenantRepository.GetAllAsync(cancellationToken);
            return tenants.Select(MapTenant).ToList();
        }

        public async Task<bool> DisableTenantAsync(
            Guid tenantId,
            CancellationToken cancellationToken = default)
        {
            EnsureAdmin();

            var tenant = await _tenantRepository.GetByIdAsync(tenantId, cancellationToken);
            if (tenant is null)
                return false;

            if (!tenant.IsActive)
                return true;

            tenant.IsActive = false;

            var updated = await _tenantRepository.UpdateAsync(tenant, cancellationToken);

            if (updated)
            {
                _logger.LogInformation(
                    "Tenant disabled. TenantId={TenantId}, TenantGuid={TenantGuid}",
                    tenant.Id,
                    tenant.ExternalTenantId);
            }

            return updated;
        }

        public async Task<CreatedApiTokenResult> CreateApiTokenAsync(
            CreateApiTokenRequest request,
            CancellationToken cancellationToken = default)
        {
            EnsureAdmin();

            if (string.IsNullOrWhiteSpace(request.Name))
                throw new ArgumentException("Token name is required.", nameof(request));

            var tenant = await _tenantRepository.GetByIdAsync(request.TenantId, cancellationToken);
            if (tenant is null)
                throw new InvalidOperationException($"Tenant {request.TenantId} not found.");

            var plainTextToken = TokenHashHelper.GenerateToken();
            var tokenHash = TokenHashHelper.ComputeSha256(plainTextToken);

            var entity = new ApiToken
            {
                TenantId = request.TenantId,
                Name = request.Name.Trim(),
                TokenHash = tokenHash,
                TokenPrefix = TokenHashHelper.GetPrefix(plainTextToken),
                IsActive = true,
                CanRead = request.CanRead,
                CanWrite = request.CanWrite,
                CanDelete = request.CanDelete,
                IsAdmin = request.IsAdmin,
                CreatedUtc = DateTime.UtcNow,
                ExpiresUtc = request.ExpiresUtc
            };

            _ = await _apiTokenRepository.InsertAsync(entity, cancellationToken);

            _logger.LogInformation(
                "API token created. TokenId={TokenId}, TenantId={TenantId}, IsAdmin={IsAdmin}, Prefix={TokenPrefix}",
                entity.Id,
                entity.TenantId,
                entity.IsAdmin,
                entity.TokenPrefix);

            return new CreatedApiTokenResult
            {
                Token = MapToken(entity),
                PlainTextToken = plainTextToken
            };
        }

        public async Task<IReadOnlyList<ApiTokenDto>> GetTenantTokensAsync(
            Guid tenantId,
            CancellationToken cancellationToken = default)
        {
            EnsureAdmin();

            var tokens = await _apiTokenRepository.GetByTenantIdAsync(tenantId, cancellationToken);
            return tokens.Select(MapToken).ToList();
        }

        public async Task<ApiTokenDto?> GetApiTokenByIdAsync(
            Guid tokenId,
            CancellationToken cancellationToken = default)
        {
            EnsureAdmin();

            var token = await _apiTokenRepository.GetByIdAsync(tokenId, cancellationToken);
            return token is null ? null : MapToken(token);
        }

        public async Task<bool> RevokeApiTokenAsync(
            Guid tokenId,
            CancellationToken cancellationToken = default)
        {
            EnsureAdmin();

            var token = await _apiTokenRepository.GetByIdAsync(tokenId, cancellationToken);
            if (token is null)
                return false;

            if (!token.IsActive && token.RevokedUtc.HasValue)
                return true;

            var result = await _apiTokenRepository.RevokeAsync(tokenId, DateTime.UtcNow, cancellationToken);

            if (result)
            {
                _logger.LogInformation(
                    "API token revoked. TokenId={TokenId}, TenantId={TenantId}",
                    token.Id,
                    token.TenantId);
            }

            return result;
        }

        public async Task<bool> DeleteApiTokenAsync(
            Guid tokenId,
            CancellationToken cancellationToken = default)
        {
            EnsureAdmin();

            return await _apiTokenRepository.DeleteAsync(tokenId, cancellationToken);
        }

        public async Task<CreatedApiTokenResult?> RotateApiTokenAsync(
            Guid tokenId,
            CancellationToken cancellationToken = default)
        {
            EnsureAdmin();

            var token = await _apiTokenRepository.GetByIdAsync(tokenId, cancellationToken);
            if (token is null)
                return null;

            if (!token.TenantId.HasValue)
                throw new InvalidOperationException("Only tenant-bound API tokens can be rotated.");

            var plainTextToken = TokenHashHelper.GenerateToken();
            var replacement = new ApiToken
            {
                TenantId = token.TenantId,
                Name = token.Name,
                TokenHash = TokenHashHelper.ComputeSha256(plainTextToken),
                TokenPrefix = TokenHashHelper.GetPrefix(plainTextToken),
                IsActive = true,
                CanRead = token.CanRead,
                CanWrite = token.CanWrite,
                CanDelete = token.CanDelete,
                IsAdmin = token.IsAdmin,
                CreatedUtc = DateTime.UtcNow,
                ExpiresUtc = token.ExpiresUtc
            };

            _ = await _apiTokenRepository.InsertAsync(replacement, cancellationToken);
            _ = await _apiTokenRepository.RevokeAsync(tokenId, DateTime.UtcNow, cancellationToken);

            _logger.LogInformation(
                "API token rotated. OldTokenId={OldTokenId}, NewTokenId={NewTokenId}, TenantId={TenantId}, Prefix={TokenPrefix}",
                token.Id,
                replacement.Id,
                replacement.TenantId,
                replacement.TokenPrefix);

            return new CreatedApiTokenResult
            {
                Token = MapToken(replacement),
                PlainTextToken = plainTextToken
            };
        }

        private void EnsureAdmin()
        {
            var current = _currentTenantAccessor.GetRequired();

            if (!current.IsAdmin)
                throw new UnauthorizedAccessException("Administrative token is required.");
        }

        private static TenantDto MapTenant(Tenant tenant)
        {
            return new TenantDto
            {
                Id = tenant.Id,
                TenantGuid = tenant.ExternalTenantId,
                Name = tenant.Name,
                IsActive = tenant.IsActive,
                CreatedUtc = tenant.CreatedUtc,
                UpdatedUtc = tenant.UpdatedUtc
            };
        }

        private static ApiTokenDto MapToken(ApiToken token)
        {
            return new ApiTokenDto
            {
                Id = token.Id,
                TenantId = token.TenantId ?? Guid.Empty,
                Name = token.Name,
                TokenPrefix = token.TokenPrefix,
                IsActive = token.IsActive,
                CanRead = token.CanRead,
                CanWrite = token.CanWrite,
                CanDelete = token.CanDelete,
                IsAdmin = token.IsAdmin,
                CreatedUtc = token.CreatedUtc,
                LastUsedUtc = token.LastUsedUtc,
                ExpiresUtc = token.ExpiresUtc,
                RevokedUtc = token.RevokedUtc
            };
        }
    }
}
