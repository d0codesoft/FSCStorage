using SCP.StorageFSC.Data.Dto;
using SCP.StorageFSC.Data.Models;
using SCP.StorageFSC.Data.Repositories;
using SCP.StorageFSC.InterfacesService;
using SCP.StorageFSC.Security;
using System.Security.Claims;
using scp.filestorage.Data.Repositories;
using scp.filestorage.Data.Models;
using scp.filestorage.Services.Auth;

namespace SCP.StorageFSC.Services
{
    public sealed class TenantStorageService : ITenantStorageService
    {
        private readonly ITenantRepository _tenantRepository;
        private readonly IApiTokenRepository _apiTokenRepository;
        private readonly IUserRepository _userRepository;
        private readonly ITenantFileRepository _tenantFileRepository;
        private readonly IStoredFileRepository _storedFileRepository;
        private readonly IDeletedTenantRepository _deletedTenantRepository;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<TenantStorageService> _logger;

        public TenantStorageService(
            ITenantRepository tenantRepository,
            IApiTokenRepository apiTokenRepository,
            IUserRepository userRepository,
            ITenantFileRepository tenantFileRepository,
            IStoredFileRepository storedFileRepository,
            IDeletedTenantRepository deletedTenantRepository,
            IHttpContextAccessor httpContextAccessor,
            ILogger<TenantStorageService> logger)
        {
            _tenantRepository = tenantRepository;
            _apiTokenRepository = apiTokenRepository;
            _userRepository = userRepository;
            _tenantFileRepository = tenantFileRepository;
            _storedFileRepository = storedFileRepository;
            _deletedTenantRepository = deletedTenantRepository;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        private async Task FillUserInformationToDtoTenant(TenantDto tenantDto, CancellationToken cancellationToken)
        {
            var user = await _userRepository.GetByIdAsync(tenantDto.UserId, cancellationToken);
            if (user is not null)
            {
                tenantDto.UserName = user.Name;
                tenantDto.UserEmail = user.Email ?? string.Empty;
                tenantDto.IsActiveUser = user.IsActive;
            }
        }

        public async Task<TenantDto> CreateTenantAsync(
            CreateTenantRequest request,
            CancellationToken cancellationToken = default)
        {
            EnsureAdmin();

            var normalizedName = ValidateTenantName(request.Name);
            var owner = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
            if (owner is null)
                throw new InvalidOperationException($"User {request.UserId} not found.");

            var existing = await _tenantRepository.GetByNameAsync(normalizedName, cancellationToken);
            if (existing is not null)
                throw new InvalidOperationException($"Tenant with name '{request.Name}' already exists.");

            var entity = new Tenant
            {
                UserId = owner.Id,
                ExternalTenantId = Guid.CreateVersion7(),
                Name = normalizedName,
                IsActive = true,
                CreatedUtc = DateTime.UtcNow
            };

            _ = await _tenantRepository.InsertAsync(entity, cancellationToken);

            _logger.LogInformation(
                "Tenant created. TenantId={TenantId}, TenantGuid={TenantGuid}, Name={TenantName}",
                entity.Id,
                entity.ExternalTenantId,
                entity.Name);

            var result = MapTenant(entity);
            await FillUserInformationToDtoTenant(result, cancellationToken);

            return result;
        }

        public async Task<TenantDto?> UpdateTenantAsync(
            Guid tenantId,
            UpdateTenantRequest request,
            CancellationToken cancellationToken = default)
        {
            EnsureAdmin();

            var tenant = await _tenantRepository.GetByIdAsync(tenantId, cancellationToken);
            if (tenant is null)
                return null;

            var normalizedName = ValidateTenantName(request.Name);

            if (!string.Equals(tenant.Name, normalizedName, StringComparison.Ordinal))
            {
                var existing = await _tenantRepository.GetByNameAsync(normalizedName, cancellationToken);
                if (existing is not null && existing.Id != tenantId)
                    throw new InvalidOperationException($"Tenant with name '{request.Name}' already exists.");
            }

            tenant.Name = normalizedName;
            tenant.IsActive = request.IsActive;
            tenant.MarkUpdated();

            var updated = await _tenantRepository.UpdateAsync(tenant, cancellationToken);
            if (!updated)
                return null;

            _logger.LogInformation(
                "Tenant updated. TenantId={TenantId}, TenantGuid={TenantGuid}, Name={TenantName}, IsActive={IsActive}",
                tenant.Id,
                tenant.ExternalTenantId,
                tenant.Name,
                tenant.IsActive);

            var result = MapTenant(tenant);
            await FillUserInformationToDtoTenant(result, cancellationToken);

            return result;
        }

        public async Task<TenantDto?> GetTenantByIdAsync(
            Guid tenantId,
            CancellationToken cancellationToken = default)
        {
            var tenant = await _tenantRepository.GetByIdAsync(tenantId, cancellationToken);
            await DemandAdminOrTenantOwnerAsync(tenant, cancellationToken);

            if (tenant is null)
                return null;

            var tenantDto = MapTenant(tenant);
            await FillUserInformationToDtoTenant(tenantDto, cancellationToken);

            return tenantDto;
        }

        public async Task<TenantDto?> GetTenantByGuidAsync(
            Guid tenantGuid,
            CancellationToken cancellationToken = default)
        {
            var tenant = await _tenantRepository.GetByGuidAsync(tenantGuid, cancellationToken);
            await DemandAdminOrTenantOwnerAsync(tenant, cancellationToken);

            if (tenant is null)
                return null;

            var tenantDto = MapTenant(tenant);
            await FillUserInformationToDtoTenant(tenantDto, cancellationToken);

            return tenantDto;
        }

        public async Task<IReadOnlyList<TenantDto>> GetTenantsAsync(
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<Tenant> tenants;

            if (IsCurrentUserAdmin())
            {
                tenants = await _tenantRepository.GetAllAsync(cancellationToken);
            }
            else
            {
                var userId = GetRequiredCurrentUserId();
                tenants = await _tenantRepository.GetByUserIdAsync(userId, cancellationToken);
            }

            var result = tenants.Select(MapTenant).ToList();

            foreach (var tenantDto in result)
            {
                await FillUserInformationToDtoTenant(tenantDto, cancellationToken);
            }

            return result;
        }

        public async Task<IReadOnlyList<StoredTenantFileDto>> GetTenantFilesAsync(
            Guid tenantId,
            CancellationToken cancellationToken = default)
        {
            var tenant = await _tenantRepository.GetByIdAsync(tenantId, cancellationToken);
            await DemandAdminOrTenantOwnerAsync(tenant, cancellationToken);

            if (tenant is null)
                return [];

            var tenantFiles = await _tenantFileRepository.GetByTenantIdAsync(
                tenant.Id,
                cancellationToken);

            var result = new List<StoredTenantFileDto>(tenantFiles.Count);

            foreach (var tenantFile in tenantFiles)
            {
                var storedFile = await _storedFileRepository.GetByIdAsync(
                    tenantFile.StoredFileId,
                    cancellationToken);

                if (storedFile is null || storedFile.IsDeleted)
                    continue;

                result.Add(MapTenantFile(tenantFile, storedFile));
            }

            return result;
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

        public async Task<bool> DeleteTenantAsync(
            Guid tenantId,
            CancellationToken cancellationToken = default)
        {
            EnsureAdmin();

            var tenant = await _tenantRepository.GetByIdAsync(tenantId, cancellationToken);
            if (tenant is null)
                return false;

            var deleted = await DeleteTenantOwnedDataAsync(tenant, cancellationToken);

            if (deleted)
            {
                _logger.LogInformation(
                    "Tenant deleted. TenantId={TenantId}",
                    tenantId);
            }

            return deleted;
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

            var effectiveUserId = request.UserId == Guid.Empty ? tenant.UserId : request.UserId;
            if (tenant.UserId != effectiveUserId)
                throw new InvalidOperationException("API token owner must match the tenant owner.");

            var plainTextToken = TokenHashHelper.GenerateToken();
            var tokenHash = TokenHashHelper.ComputeSha256(plainTextToken);

            var entity = new ApiToken
            {
                UserId = effectiveUserId,
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

        public async Task<CreatedApiTokenResult?> CreateTenantApiTokenAsync(
            Guid tenantId,
            CreateTenantApiTokenRequest request,
            CancellationToken cancellationToken = default)
        {
            EnsureAdmin();

            var tenant = await _tenantRepository.GetByIdAsync(tenantId, cancellationToken);
            if (tenant is null)
                return null;

            return await CreateApiTokenAsync(new CreateApiTokenRequest
            {
                UserId = tenant.UserId,
                TenantId = tenant.Id,
                Name = request.Name,
                CanRead = request.CanRead,
                CanWrite = request.CanWrite,
                CanDelete = request.CanDelete,
                IsAdmin = request.IsAdmin,
                ExpiresUtc = request.ExpiresUtc
            }, cancellationToken);
        }

        public async Task<ApiTokenDto?> UpdateApiTokenAsync(
            Guid tokenId,
            UpdateApiTokenRequest request,
            CancellationToken cancellationToken = default)
        {
            EnsureAdmin();

            var token = await _apiTokenRepository.GetByIdAsync(tokenId, cancellationToken);
            if (token is null)
                return null;

            token.Name = ValidateTokenName(request.Name);
            token.CanRead = request.CanRead;
            token.CanWrite = request.CanWrite;
            token.CanDelete = request.CanDelete;
            token.IsAdmin = request.IsAdmin;
            token.IsActive = request.IsActive;
            token.ExpiresUtc = request.ExpiresUtc;
            token.RevokedUtc = request.IsActive ? null : token.RevokedUtc ?? DateTime.UtcNow;
            token.MarkUpdated();

            var updated = await _apiTokenRepository.UpdateAsync(token, cancellationToken);
            if (!updated)
                return null;

            _logger.LogInformation(
                "API token updated. TokenId={TokenId}, TenantId={TenantId}, IsActive={IsActive}, IsAdmin={IsAdmin}",
                token.Id,
                token.TenantId,
                token.IsActive,
                token.IsAdmin);

            return MapToken(token);
        }

        public async Task<IReadOnlyList<ApiTokenDto>> GetTenantTokensAsync(
            Guid tenantId,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<ApiToken> tokens;

            if (IsCurrentUserAdmin())
            {
                tokens = await _apiTokenRepository.GetByTenantIdAsync(tenantId, cancellationToken);
            }
            else
            {
                var tenant = await _tenantRepository.GetByIdAsync(tenantId, cancellationToken);
                await DemandAdminOrTenantOwnerAsync(tenant, cancellationToken);

                var userId = GetRequiredCurrentUserId();
                tokens = await _apiTokenRepository.GetByTenantIdAndUserIdAsync(tenantId, userId, cancellationToken);
            }

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

        public async Task<bool> DeleteTenantApiTokenAsync(
            Guid tenantId,
            Guid tokenId,
            CancellationToken cancellationToken = default)
        {
            EnsureAdmin();

            var token = await _apiTokenRepository.GetByIdAsync(tokenId, cancellationToken);
            if (token is null || token.TenantId != tenantId)
                return false;

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
            if (!IsCurrentUserAdmin())
                throw new UnauthorizedAccessException("Administrative token is required.");
        }

        private bool IsCurrentUserAdmin()
        {
            return _httpContextAccessor.HttpContext?.User?.IsInRole("Admin") == true;
        }

        private Guid GetRequiredCurrentUserId()
        {
            var user = _httpContextAccessor.HttpContext?.User
                ?? throw new UnauthorizedAccessException("Authenticated user is required.");

            if (!Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
                throw new UnauthorizedAccessException("Authenticated user identifier is missing.");

            return userId;
        }

        private async Task DemandAdminOrTenantOwnerAsync(Tenant? tenant, CancellationToken cancellationToken)
        {
            if (tenant is null)
                return;

            if (IsCurrentUserAdmin())
                return;

            var userId = GetRequiredCurrentUserId();
            if (tenant.UserId == userId)
                return;

            await Task.CompletedTask;
            throw new UnauthorizedAccessException("Access to another user's tenant is denied.");
        }

        private static string ValidateTenantName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Tenant name is required.", nameof(name));

            return name.Trim();
        }
        private static string ValidateTokenName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Token name is required.", nameof(name));

            return name.Trim();
        }
        private async Task<bool> DeleteTenantOwnedDataAsync(Tenant tenant, CancellationToken cancellationToken)
        {
            var tenantFiles = await _tenantFileRepository.GetByTenantIdAsync(tenant.Id, cancellationToken);
            var storedFileUsage = tenantFiles
                .GroupBy(file => file.StoredFileId)
                .Select(group => new { StoredFileId = group.Key, Count = group.Count() })
                .ToList();

            foreach (var usage in storedFileUsage)
            {
                await _storedFileRepository.DecrementReferenceCountAsync(
                    usage.StoredFileId,
                    usage.Count,
                    cancellationToken);
            }

            await _deletedTenantRepository.InsertAsync(new DeletedTenant
            {
                TenantId = tenant.Id,
                UserId = tenant.UserId,
                TenantGuid = tenant.ExternalTenantId,
                TenantName = tenant.Name,
                DeletedUtc = DateTime.UtcNow,
                CreatedUtc = DateTime.UtcNow
            }, cancellationToken);

            return await _tenantRepository.DeleteAsync(tenant.Id, cancellationToken);
        }
        private static TenantDto MapTenant(Tenant tenant)
        {
            return new TenantDto
            {
                Id = tenant.Id,
                UserId = tenant.UserId,
                TenantGuid = tenant.ExternalTenantId,
                Name = tenant.Name,
                IsActive = tenant.IsActive,
                CreatedUtc = tenant.CreatedUtc,
                UpdatedUtc = tenant.UpdatedUtc
            };
        }

        private static StoredTenantFileDto MapTenantFile(TenantFile tenantFile, StoredFile storedFile)
        {
            return new StoredTenantFileDto
            {
                TenantFileId = tenantFile.Id,
                FileGuid = tenantFile.FileGuid,
                TenantId = tenantFile.TenantId,
                StoredFileId = tenantFile.StoredFileId,
                FileName = tenantFile.FileName,
                Category = tenantFile.Category,
                ExternalKey = tenantFile.ExternalKey,
                ContentType = storedFile.ContentType,
                StateCompress = storedFile.StateCompress,
                FileSize = storedFile.FileSize,
                Sha256 = storedFile.Sha256,
                Crc32 = storedFile.Crc32,
                CreatedUtc = tenantFile.CreatedUtc
            };
        }

        private static ApiTokenDto MapToken(ApiToken token)
        {
            return new ApiTokenDto
            {
                Id = token.Id,
                UserId = token.UserId,
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
