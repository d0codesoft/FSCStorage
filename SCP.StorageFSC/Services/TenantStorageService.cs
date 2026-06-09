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
        private readonly IUserRoleRepository _userRoleRepository;
        private readonly ITenantFileRepository _tenantFileRepository;
        private readonly IStoredFileRepository _storedFileRepository;
        private readonly IDeletedTenantRepository _deletedTenantRepository;
        private readonly IPasswordHashService _passwordHashService;
        private readonly IUserTwoFactorMethodRepository _twoFactorMethodRepository;
        private readonly IUserRecoveryCodeRepository _recoveryCodeRepository;
        private readonly IAuthenticationHashService _authenticationHashService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<TenantStorageService> _logger;

        public TenantStorageService(
            ITenantRepository tenantRepository,
            IApiTokenRepository apiTokenRepository,
            IUserRepository userRepository,
            IUserRoleRepository userRoleRepository,
            ITenantFileRepository tenantFileRepository,
            IStoredFileRepository storedFileRepository,
            IDeletedTenantRepository deletedTenantRepository,
            IPasswordHashService passwordHashService,
            IUserTwoFactorMethodRepository twoFactorMethodRepository,
            IUserRecoveryCodeRepository recoveryCodeRepository,
            IAuthenticationHashService authenticationHashService,
            IHttpContextAccessor httpContextAccessor,
            ILogger<TenantStorageService> logger)
        {
            _tenantRepository = tenantRepository;
            _apiTokenRepository = apiTokenRepository;
            _userRepository = userRepository;
            _userRoleRepository = userRoleRepository;
            _tenantFileRepository = tenantFileRepository;
            _storedFileRepository = storedFileRepository;
            _deletedTenantRepository = deletedTenantRepository;
            _passwordHashService = passwordHashService;
            _twoFactorMethodRepository = twoFactorMethodRepository;
            _recoveryCodeRepository = recoveryCodeRepository;
            _authenticationHashService = authenticationHashService;
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

        public async Task<IReadOnlyList<UserTenantsDto>> GetUsersWithTenantsAsync(
            CancellationToken cancellationToken = default)
        {
            EnsureAdmin();

            var users = await _userRepository.GetAllAsync(cancellationToken);
            var tenants = await _tenantRepository.GetAllAsync(cancellationToken);
            var tenantsByUserId = tenants
                .GroupBy(tenant => tenant.UserId)
                .ToDictionary(group => group.Key, group => (IReadOnlyList<TenantDto>)group.Select(MapTenant).ToList());

            foreach (var tenantList in tenantsByUserId.Values)
            {
                foreach (var tenantDto in tenantList)
                {
                    await FillUserInformationToDtoTenant(tenantDto, cancellationToken);
                }
            }

            return users
                .Select(user => new UserTenantsDto
                {
                    UserId = user.Id,
                    UserName = user.Name,
                    Email = user.Email,
                    PhoneNumber = user.PhoneNumber,
                    PhoneNumberConfirmed = user.PhoneNumberConfirmed,
                    IsActive = user.IsActive,
                    IsLocked = user.IsLocked && (!user.LockedUntilUtc.HasValue || user.LockedUntilUtc > DateTime.UtcNow),
                    LockedUntilUtc = user.LockedUntilUtc,
                    FailedLoginCount = user.FailedLoginCount,
                    LastFailedLoginUtc = user.LastFailedLoginUtc,
                    LastLoginUtc = user.LastLoginUtc,
                    LastLoginIpAddress = user.LastLoginIpAddress,
                    TwoFactorEnabled = user.TwoFactorEnabled,
                    TwoFactorRequiredForEveryLogin = user.TwoFactorRequiredForEveryLogin,
                    PreferredTwoFactorMethod = user.PreferredTwoFactorMethod,
                    TwoFactorEnabledUtc = user.TwoFactorEnabledUtc,
                    TwoFactorLastUsedUtc = user.TwoFactorLastUsedUtc,
                    MustChangePassword = user.MustChangePassword,
                    PasswordChangedUtc = user.PasswordChangedUtc,
                    PasswordExpiresUtc = user.PasswordExpiresUtc,
                    ExternalUserId = user.ExternalUserId,
                    Comment = user.Comment,
                    Tenants = tenantsByUserId.TryGetValue(user.Id, out var userTenants)
                        ? userTenants
                        : []
                })
                .ToList();
        }

        public async Task<IReadOnlyList<UserManagementDto>> GetUsersAsync(
            CancellationToken cancellationToken = default)
        {
            EnsureAdmin();

            var users = await _userRepository.GetAllAsync(cancellationToken);
            var tenants = await _tenantRepository.GetAllAsync(cancellationToken);
            var tokens = new List<ApiToken>();

            foreach (var user in users)
            {
                tokens.AddRange(await _apiTokenRepository.GetByUserIdAsync(user.Id, cancellationToken));
            }

            var rolesByUserId = new Dictionary<Guid, bool>();
            foreach (var user in users)
            {
                rolesByUserId[user.Id] = await _userRoleRepository.UserHasRoleAsync(
                    user.Id,
                    SystemRoles.AdministratorId,
                    cancellationToken);
            }

            var tenantsByUserId = tenants
                .GroupBy(tenant => tenant.UserId)
                .ToDictionary(group => group.Key, group => (IReadOnlyList<TenantDto>)group.Select(MapTenant).ToList());

            foreach (var tenantList in tenantsByUserId.Values)
            {
                foreach (var tenantDto in tenantList)
                {
                    await FillUserInformationToDtoTenant(tenantDto, cancellationToken);
                }
            }

            var tenantNamesById = tenants.ToDictionary(x => x.Id, x => x.Name);
            var tokensByUserId = tokens
                .GroupBy(token => token.UserId)
                .ToDictionary(
                    group => group.Key,
                    group => (IReadOnlyList<UserApiTokenDto>)group
                        .Select(token => MapUserToken(
                            token,
                            token.TenantId.HasValue && tenantNamesById.TryGetValue(token.TenantId.Value, out var tenantName)
                                ? tenantName
                                : string.Empty))
                        .ToList());

            return users
                .Select(user => new UserManagementDto
                {
                    UserId = user.Id,
                    UserName = user.Name,
                    Email = user.Email,
                    PhoneNumber = user.PhoneNumber,
                    PhoneNumberConfirmed = user.PhoneNumberConfirmed,
                    IsActive = user.IsActive,
                    IsLocked = user.IsLocked && (!user.LockedUntilUtc.HasValue || user.LockedUntilUtc > DateTime.UtcNow),
                    LockedUntilUtc = user.LockedUntilUtc,
                    FailedLoginCount = user.FailedLoginCount,
                    LastFailedLoginUtc = user.LastFailedLoginUtc,
                    LastLoginUtc = user.LastLoginUtc,
                    LastLoginIpAddress = user.LastLoginIpAddress,
                    TwoFactorEnabled = user.TwoFactorEnabled,
                    TwoFactorRequiredForEveryLogin = user.TwoFactorRequiredForEveryLogin,
                    PreferredTwoFactorMethod = user.PreferredTwoFactorMethod,
                    TwoFactorEnabledUtc = user.TwoFactorEnabledUtc,
                    TwoFactorLastUsedUtc = user.TwoFactorLastUsedUtc,
                    MustChangePassword = user.MustChangePassword,
                    PasswordChangedUtc = user.PasswordChangedUtc,
                    PasswordExpiresUtc = user.PasswordExpiresUtc,
                    ExternalUserId = user.ExternalUserId,
                    Comment = user.Comment,
                    IsAdmin = rolesByUserId.TryGetValue(user.Id, out var isAdmin) && isAdmin,
                    CreatedUtc = user.CreatedUtc,
                    UpdatedUtc = user.UpdatedUtc,
                    Tenants = tenantsByUserId.TryGetValue(user.Id, out var userTenants) ? userTenants : [],
                    ApiTokens = tokensByUserId.TryGetValue(user.Id, out var userTokens) ? userTokens : []
                })
                .ToList();
        }

        public async Task<UserManagementDto> CreateUserAsync(
            CreateUserRequest request,
            CancellationToken cancellationToken = default)
        {
            EnsureAdmin();

            var userName = ValidateUserName(request.Name);
            var normalizedUserName = Normalize(userName);
            var normalizedEmail = NormalizeEmail(request.Email);

            if (await _userRepository.GetByNormalizedNameAsync(normalizedUserName, cancellationToken) is not null)
                throw new InvalidOperationException($"User with name '{request.Name}' already exists.");

            if (normalizedEmail is not null &&
                await _userRepository.GetByNormalizedEmailAsync(normalizedEmail, cancellationToken) is not null)
            {
                throw new InvalidOperationException($"User with email '{request.Email}' already exists.");
            }

            var user = new User
            {
                Name = userName,
                NormalizedName = normalizedUserName,
                Email = NormalizeNullableText(request.Email),
                NormalizedEmail = normalizedEmail,
                PhoneNumber = NormalizeNullableText(request.PhoneNumber),
                PasswordHash = string.Empty,
                IsActive = request.IsActive,
                MustChangePassword = request.MustChangePassword,
                TwoFactorEnabled = false,
                TwoFactorRequiredForEveryLogin = false,
                PreferredTwoFactorMethod = TwoFactorMethodType.None,
                CreatedUtc = DateTime.UtcNow
            };

            var password = string.IsNullOrWhiteSpace(request.TemporaryPassword)
                ? request.Password
                : request.TemporaryPassword;
            if (string.IsNullOrWhiteSpace(password))
                throw new ArgumentException("Temporary password is required.", nameof(request));

            user.PasswordHash = _passwordHashService.HashPassword(user, password);
            user.PasswordChangedUtc = DateTime.UtcNow;

            var created = await _userRepository.InsertAsync(user, cancellationToken);
            if (!created)
                throw new InvalidOperationException("Failed to create user.");

            if (request.IsAdmin)
            {
                await EnsureAdminRoleAsync(user.Id, shouldBeAdmin: true, cancellationToken);
            }

            _logger.LogInformation(
                "User created. UserId={UserId}, UserName={UserName}, IsAdmin={IsAdmin}",
                user.Id,
                user.Name,
                request.IsAdmin);

            return await GetUserManagementAsync(user, cancellationToken);
        }

        public async Task<UserManagementDto?> GetUserAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            EnsureAdmin();

            var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
            return user is null ? null : await GetUserManagementAsync(user, cancellationToken);
        }

        public async Task<UserManagementDto?> UpdateUserAsync(
            Guid userId,
            UpdateUserRequest request,
            CancellationToken cancellationToken = default)
        {
            return await UpdateUserProfileAsync(userId, request, cancellationToken);
        }

        public async Task<UserManagementDto?> UpdateUserProfileAsync(
            Guid userId,
            UpdateUserProfileRequest request,
            CancellationToken cancellationToken = default)
        {
            EnsureAdmin();

            var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
            if (user is null)
                return null;

            var userName = ValidateUserName(request.Name);
            var normalizedUserName = Normalize(userName);
            var normalizedEmail = NormalizeEmail(request.Email);

            var existingByName = await _userRepository.GetByNormalizedNameAsync(normalizedUserName, cancellationToken);
            if (existingByName is not null && existingByName.Id != user.Id)
                throw new InvalidOperationException($"User with name '{request.Name}' already exists.");

            if (normalizedEmail is not null)
            {
                var existingByEmail = await _userRepository.GetByNormalizedEmailAsync(normalizedEmail, cancellationToken);
                if (existingByEmail is not null && existingByEmail.Id != user.Id)
                    throw new InvalidOperationException($"User with email '{request.Email}' already exists.");
            }

            user.Name = userName;
            user.NormalizedName = normalizedUserName;
            var email = NormalizeNullableText(request.Email);
            if (!string.Equals(user.Email, email, StringComparison.Ordinal))
            {
                user.EmailConfirmed = false;
                user.SecurityStamp = Guid.NewGuid().ToString("N");
            }

            user.Email = email;
            user.NormalizedEmail = normalizedEmail;
            user.PhoneNumber = NormalizeNullableText(request.PhoneNumber);
            user.ExternalUserId = NormalizeNullableText(request.ExternalUserId);
            user.Comment = NormalizeNullableText(request.Comment);

            user.MarkUpdated();

            var updated = await _userRepository.UpdateAsync(user, cancellationToken);
            if (!updated)
                return null;

            _logger.LogInformation(
                "User updated. UserId={UserId}, UserName={UserName}, IsActive={IsActive}, IsAdmin={IsAdmin}",
                user.Id,
                user.Name,
                user.IsActive,
                await _userRoleRepository.UserHasRoleAsync(user.Id, SystemRoles.AdministratorId, cancellationToken));

            return await GetUserManagementAsync(user, cancellationToken);
        }

        public async Task<bool> SetUserBlockedAsync(
            Guid userId,
            bool isBlocked,
            CancellationToken cancellationToken = default)
        {
            EnsureAdmin();

            var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
            if (user is null)
                return false;

            user.IsLocked = isBlocked;
            user.LockedUntilUtc = isBlocked ? DateTime.UtcNow.AddYears(100) : null;
            user.SecurityStamp = Guid.NewGuid().ToString("N");
            user.MarkUpdated();

            var updated = await _userRepository.UpdateAsync(user, cancellationToken);
            if (updated)
            {
                _logger.LogInformation(
                    "User block state updated. UserId={UserId}, IsBlocked={IsBlocked}",
                    user.Id,
                    isBlocked);
            }

            return updated;
        }

        public async Task<bool> ActivateUserAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            return await SetUserActiveAsync(userId, true, cancellationToken);
        }

        public async Task<bool> DeactivateUserAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            return await SetUserActiveAsync(userId, false, cancellationToken);
        }

        public async Task<bool> LockUserAsync(
            Guid userId,
            DateTime lockedUntilUtc,
            CancellationToken cancellationToken = default)
        {
            EnsureAdmin();

            var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
            if (user is null)
                return false;

            user.IsLocked = true;
            user.LockedUntilUtc = lockedUntilUtc.Kind == DateTimeKind.Utc
                ? lockedUntilUtc
                : lockedUntilUtc.ToUniversalTime();
            user.SecurityStamp = Guid.NewGuid().ToString("N");
            user.MarkUpdated();

            return await _userRepository.UpdateAsync(user, cancellationToken);
        }

        public async Task<bool> UnlockUserAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            EnsureAdmin();

            var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
            if (user is null)
                return false;

            user.IsLocked = false;
            user.LockedUntilUtc = null;
            user.FailedLoginCount = 0;
            user.LastFailedLoginUtc = null;
            user.SecurityStamp = Guid.NewGuid().ToString("N");
            user.MarkUpdated();

            return await _userRepository.UpdateAsync(user, cancellationToken);
        }

        public async Task<bool> ResetFailedLoginCountAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            EnsureAdmin();

            var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
            if (user is null)
                return false;

            user.FailedLoginCount = 0;
            user.LastFailedLoginUtc = null;
            user.MarkUpdated();

            return await _userRepository.UpdateAsync(user, cancellationToken);
        }

        public async Task<bool> ResetUserPasswordAsync(
            Guid userId,
            ResetUserPasswordRequest request,
            CancellationToken cancellationToken = default)
        {
            EnsureAdmin();

            if (string.IsNullOrWhiteSpace(request.NewPassword))
                throw new ArgumentException("New password is required.", nameof(request));

            var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
            if (user is null)
                return false;

            user.PasswordHash = _passwordHashService.HashPassword(user, request.NewPassword);
            user.PasswordChangedUtc = DateTime.UtcNow;
            user.SecurityStamp = Guid.NewGuid().ToString("N");
            user.FailedLoginCount = 0;
            user.LastFailedLoginUtc = null;
            user.MustChangePassword = request.MustChangePassword;
            user.PasswordExpiresUtc = null;
            user.MarkUpdated();

            return await _userRepository.UpdateAsync(user, cancellationToken);
        }

        public async Task<bool> ExpireUserPasswordAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            EnsureAdmin();

            var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
            if (user is null)
                return false;

            user.MustChangePassword = true;
            user.PasswordExpiresUtc = DateTime.UtcNow;
            user.MarkUpdated();

            return await _userRepository.UpdateAsync(user, cancellationToken);
        }

        public async Task<bool> ChangeUserEmailAsync(Guid userId, string email, CancellationToken cancellationToken = default)
        {
            EnsureAdmin();

            var normalizedEmail = NormalizeEmail(email)
                ?? throw new ArgumentException("Email is required.", nameof(email));

            var existing = await _userRepository.GetByNormalizedEmailAsync(normalizedEmail, cancellationToken);
            if (existing is not null && existing.Id != userId)
                throw new InvalidOperationException($"User with email '{email}' already exists.");

            var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
            if (user is null)
                return false;

            user.Email = NormalizeNullableText(email);
            user.NormalizedEmail = normalizedEmail;
            user.EmailConfirmed = false;
            user.SecurityStamp = Guid.NewGuid().ToString("N");
            user.MarkUpdated();

            return await _userRepository.UpdateAsync(user, cancellationToken);
        }

        public async Task<bool> SetUserEmailConfirmedAsync(Guid userId, bool confirmed, CancellationToken cancellationToken = default)
        {
            EnsureAdmin();

            var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
            if (user is null)
                return false;

            user.EmailConfirmed = confirmed;
            user.MarkUpdated();

            return await _userRepository.UpdateAsync(user, cancellationToken);
        }

        public async Task<bool> ChangeUserPhoneAsync(Guid userId, string phoneNumber, CancellationToken cancellationToken = default)
        {
            EnsureAdmin();

            if (string.IsNullOrWhiteSpace(phoneNumber))
                throw new ArgumentException("Phone number is required.", nameof(phoneNumber));

            var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
            if (user is null)
                return false;

            user.PhoneNumber = NormalizeNullableText(phoneNumber);
            user.PhoneNumberConfirmed = false;
            user.SecurityStamp = Guid.NewGuid().ToString("N");
            user.MarkUpdated();

            return await _userRepository.UpdateAsync(user, cancellationToken);
        }

        public async Task<bool> SetUserPhoneConfirmedAsync(Guid userId, bool confirmed, CancellationToken cancellationToken = default)
        {
            EnsureAdmin();

            var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
            if (user is null)
                return false;

            user.PhoneNumberConfirmed = confirmed;
            user.MarkUpdated();

            return await _userRepository.UpdateAsync(user, cancellationToken);
        }

        public async Task<UserTwoFactorStatusDto?> GetUserTwoFactorStatusAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            EnsureAdmin();

            var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
            if (user is null)
                return null;

            var methods = await _twoFactorMethodRepository.GetByUserIdAsync(userId, cancellationToken);
            return new UserTwoFactorStatusDto
            {
                UserId = user.Id,
                TwoFactorEnabled = user.TwoFactorEnabled,
                TwoFactorRequiredForEveryLogin = user.TwoFactorRequiredForEveryLogin,
                PreferredTwoFactorMethod = user.PreferredTwoFactorMethod,
                TwoFactorEnabledUtc = user.TwoFactorEnabledUtc,
                TwoFactorLastUsedUtc = user.TwoFactorLastUsedUtc,
                Methods = methods.Select(method => new UserTwoFactorMethodDto
                {
                    Id = method.Id,
                    MethodType = method.MethodType,
                    IsEnabled = method.IsEnabled,
                    IsConfirmed = method.IsConfirmed,
                    IsDefault = method.IsDefault,
                    MaskedDestination = method.MaskedDestination,
                    ConfirmedUtc = method.ConfirmedUtc,
                    LastUsedUtc = method.LastUsedUtc
                }).ToList()
            };
        }

        public async Task<bool> EnableUserTwoFactorAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            EnsureAdmin();

            var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
            if (user is null)
                return false;

            user.TwoFactorEnabled = true;
            user.TwoFactorEnabledUtc ??= DateTime.UtcNow;
            user.MarkUpdated();

            return await _userRepository.UpdateAsync(user, cancellationToken);
        }

        public async Task<bool> DisableUserTwoFactorAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            EnsureAdmin();

            var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
            if (user is null)
                return false;

            var methods = await _twoFactorMethodRepository.GetByUserIdAsync(user.Id, cancellationToken);
            foreach (var method in methods)
            {
                method.IsEnabled = false;
                method.IsDefault = false;
                method.MarkUpdated();
                await _twoFactorMethodRepository.UpdateAsync(method, cancellationToken);
            }

            user.TwoFactorEnabled = false;
            user.TwoFactorEnabledUtc = null;
            user.TwoFactorLastUsedUtc = null;
            user.SecurityStamp = Guid.NewGuid().ToString("N");
            user.MarkUpdated();

            return await _userRepository.UpdateAsync(user, cancellationToken);
        }

        public async Task<bool> SetUserPreferredTwoFactorMethodAsync(
            Guid userId,
            SetPreferredTwoFactorMethodRequest request,
            CancellationToken cancellationToken = default)
        {
            EnsureAdmin();

            var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
            if (user is null)
                return false;

            user.PreferredTwoFactorMethod = request.PreferredTwoFactorMethod;
            user.MarkUpdated();

            return await _userRepository.UpdateAsync(user, cancellationToken);
        }

        public async Task<bool> SetUserTwoFactorRequiredForEveryLoginAsync(
            Guid userId,
            bool required,
            CancellationToken cancellationToken = default)
        {
            EnsureAdmin();

            var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
            if (user is null)
                return false;

            user.TwoFactorRequiredForEveryLogin = required;
            user.MarkUpdated();

            return await _userRepository.UpdateAsync(user, cancellationToken);
        }

        public async Task<bool> ResetUserTwoFactorAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            EnsureAdmin();

            var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
            if (user is null)
                return false;

            var methods = await _twoFactorMethodRepository.GetByUserIdAsync(user.Id, cancellationToken);
            foreach (var method in methods)
            {
                await _twoFactorMethodRepository.DeleteAsync(method.Id, cancellationToken);
            }

            await _recoveryCodeRepository.DeleteByUserIdAsync(user.Id, cancellationToken);

            user.TwoFactorEnabled = false;
            user.TwoFactorEnabledUtc = null;
            user.TwoFactorLastUsedUtc = null;
            user.PreferredTwoFactorMethod = TwoFactorMethodType.None;
            user.SecurityStamp = Guid.NewGuid().ToString("N");
            user.MarkUpdated();

            return await _userRepository.UpdateAsync(user, cancellationToken);
        }

        public async Task<string[]> RegenerateUserRecoveryCodesAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            EnsureAdmin();

            var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
            if (user is null)
                return [];

            var codes = Enumerable.Range(0, 10)
                .Select(_ => CreateRecoveryCode())
                .ToArray();

            await _recoveryCodeRepository.DeleteByUserIdAsync(user.Id, cancellationToken);
            await _recoveryCodeRepository.InsertManyAsync(
                codes.Select(code => new UserRecoveryCode
                {
                    UserId = user.Id,
                    CodeHash = _authenticationHashService.HashSecret(code),
                    CreatedUtc = DateTime.UtcNow
                }),
                cancellationToken);

            user.SecurityStamp = Guid.NewGuid().ToString("N");
            user.MarkUpdated();
            await _userRepository.UpdateAsync(user, cancellationToken);

            return codes;
        }

        public async Task<bool> RefreshUserSecurityStampAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            EnsureAdmin();

            var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
            if (user is null)
                return false;

            user.SecurityStamp = Guid.NewGuid().ToString("N");
            user.MarkUpdated();

            return await _userRepository.UpdateAsync(user, cancellationToken);
        }

        public async Task<IReadOnlyList<UserSessionDto>?> GetUserSessionsAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            EnsureAdmin();
            return await _userRepository.GetByIdAsync(userId, cancellationToken) is null
                ? null
                : [];
        }

        public async Task<IReadOnlyList<UserLoginHistoryDto>?> GetUserLoginHistoryAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            EnsureAdmin();

            var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
            if (user is null)
                return null;

            return
            [
                new UserLoginHistoryDto
                {
                    LastLoginUtc = user.LastLoginUtc,
                    LastLoginIpAddress = user.LastLoginIpAddress,
                    LastFailedLoginUtc = user.LastFailedLoginUtc,
                    FailedLoginCount = user.FailedLoginCount,
                    TwoFactorLastUsedUtc = user.TwoFactorLastUsedUtc,
                    PasswordChangedUtc = user.PasswordChangedUtc
                }
            ];
        }

        public async Task<IReadOnlyList<UserSecurityEventDto>?> GetUserSecurityEventsAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            EnsureAdmin();

            var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
            if (user is null)
                return null;

            var events = new List<UserSecurityEventDto>();
            AddSecurityEvent(events, "UserCreated", user.CreatedUtc, "User account was created.");
            AddSecurityEvent(events, "UserUpdated", user.UpdatedUtc, "User account was updated.");
            AddSecurityEvent(events, "PasswordChanged", user.PasswordChangedUtc, "User password was changed.");
            AddSecurityEvent(events, "LastLogin", user.LastLoginUtc, "User completed a login.");
            AddSecurityEvent(events, "LastFailedLogin", user.LastFailedLoginUtc, "User had a failed login attempt.");
            AddSecurityEvent(events, "TwoFactorLastUsed", user.TwoFactorLastUsedUtc, "Two-factor authentication was used.");

            return events.OrderByDescending(item => item.OccurredUtc).ToList();
        }

        public Task<IReadOnlyList<UserSecurityEventDto>?> GetUserAuditAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            return GetUserSecurityEventsAsync(userId, cancellationToken);
        }

        public async Task<bool> IsUserNameUniqueAsync(string name, Guid? excludingUserId = null, CancellationToken cancellationToken = default)
        {
            EnsureAdmin();

            var normalizedName = Normalize(ValidateUserName(name));
            var user = await _userRepository.GetByNormalizedNameAsync(normalizedName, cancellationToken);
            return user is null || user.Id == excludingUserId;
        }

        public async Task<bool> IsUserEmailUniqueAsync(string email, Guid? excludingUserId = null, CancellationToken cancellationToken = default)
        {
            EnsureAdmin();

            var normalizedEmail = NormalizeEmail(email)
                ?? throw new ArgumentException("Email is required.", nameof(email));

            var user = await _userRepository.GetByNormalizedEmailAsync(normalizedEmail, cancellationToken);
            return user is null || user.Id == excludingUserId;
        }

        public async Task<bool> DeleteUserAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            EnsureAdmin();

            if (GetRequiredCurrentUserId() == userId)
                throw new InvalidOperationException("The current administrator cannot delete their own account.");

            var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
            if (user is null)
                return false;

            if (await _userRoleRepository.UserHasRoleAsync(userId, SystemRoles.AdministratorId, cancellationToken))
            {
                await EnsureMoreThanOneAdminAsync(userId, cancellationToken);
            }

            var tenants = await _tenantRepository.GetByUserIdAsync(userId, cancellationToken);

            foreach (var tenant in tenants)
            {
                await DeleteTenantOwnedDataAsync(tenant, cancellationToken);
            }

            var remainingTokens = await _apiTokenRepository.GetByUserIdAsync(userId, cancellationToken);
            foreach (var token in remainingTokens)
            {
                await _apiTokenRepository.DeleteAsync(token.Id, cancellationToken);
            }

            var deleted = await _userRepository.DeleteAsync(userId, cancellationToken);
            if (deleted)
            {
                _logger.LogInformation(
                    "User deleted. UserId={UserId}, UserName={UserName}, DeletedTenantCount={DeletedTenantCount}",
                    user.Id,
                    user.Name,
                    tenants.Count);
            }

            return deleted;
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

        private async Task<bool> SetUserActiveAsync(
            Guid userId,
            bool isActive,
            CancellationToken cancellationToken)
        {
            EnsureAdmin();

            var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
            if (user is null)
                return false;

            if (!isActive && await _userRoleRepository.UserHasRoleAsync(userId, SystemRoles.AdministratorId, cancellationToken))
            {
                await EnsureMoreThanOneAdminAsync(userId, cancellationToken);
            }

            user.IsActive = isActive;
            if (!isActive)
            {
                user.SecurityStamp = Guid.NewGuid().ToString("N");
            }

            user.MarkUpdated();

            return await _userRepository.UpdateAsync(user, cancellationToken);
        }

        private static void AddSecurityEvent(
            ICollection<UserSecurityEventDto> events,
            string eventType,
            DateTime? occurredUtc,
            string description)
        {
            if (!occurredUtc.HasValue)
                return;

            events.Add(new UserSecurityEventDto
            {
                EventType = eventType,
                OccurredUtc = occurredUtc,
                Description = description
            });
        }

        private static string CreateRecoveryCode()
        {
            Span<byte> bytes = stackalloc byte[9];
            System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
            return Convert.ToHexString(bytes).ToLowerInvariant();
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

        private static string ValidateUserName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("User name is required.", nameof(name));

            return name.Trim();
        }

        private static string ValidateTokenName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Token name is required.", nameof(name));

            return name.Trim();
        }

        private async Task<UserManagementDto> GetUserManagementAsync(User user, CancellationToken cancellationToken)
        {
            var tenants = await _tenantRepository.GetByUserIdAsync(user.Id, cancellationToken);
            var tokens = await _apiTokenRepository.GetByUserIdAsync(user.Id, cancellationToken);
            var isAdmin = await _userRoleRepository.UserHasRoleAsync(user.Id, SystemRoles.AdministratorId, cancellationToken);
            var tenantNamesById = tenants.ToDictionary(x => x.Id, x => x.Name);

            return new UserManagementDto
            {
                UserId = user.Id,
                UserName = user.Name,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                PhoneNumberConfirmed = user.PhoneNumberConfirmed,
                IsActive = user.IsActive,
                IsLocked = user.IsLocked && (!user.LockedUntilUtc.HasValue || user.LockedUntilUtc > DateTime.UtcNow),
                LockedUntilUtc = user.LockedUntilUtc,
                FailedLoginCount = user.FailedLoginCount,
                LastFailedLoginUtc = user.LastFailedLoginUtc,
                LastLoginUtc = user.LastLoginUtc,
                LastLoginIpAddress = user.LastLoginIpAddress,
                TwoFactorEnabled = user.TwoFactorEnabled,
                TwoFactorRequiredForEveryLogin = user.TwoFactorRequiredForEveryLogin,
                PreferredTwoFactorMethod = user.PreferredTwoFactorMethod,
                TwoFactorEnabledUtc = user.TwoFactorEnabledUtc,
                TwoFactorLastUsedUtc = user.TwoFactorLastUsedUtc,
                MustChangePassword = user.MustChangePassword,
                PasswordChangedUtc = user.PasswordChangedUtc,
                PasswordExpiresUtc = user.PasswordExpiresUtc,
                ExternalUserId = user.ExternalUserId,
                Comment = user.Comment,
                IsAdmin = isAdmin,
                CreatedUtc = user.CreatedUtc,
                UpdatedUtc = user.UpdatedUtc,
                Tenants = tenants.Select(MapTenant).ToList(),
                ApiTokens = tokens
                    .Select(token => MapUserToken(
                        token,
                        token.TenantId.HasValue && tenantNamesById.TryGetValue(token.TenantId.Value, out var tenantName)
                            ? tenantName
                            : string.Empty))
                    .ToList()
            };
        }

        private async Task EnsureAdminRoleAsync(Guid userId, bool shouldBeAdmin, CancellationToken cancellationToken)
        {
            var existingAssignment = await _userRoleRepository.GetByUserIdAndRoleIdAsync(
                userId,
                SystemRoles.AdministratorId,
                cancellationToken);

            if (shouldBeAdmin)
            {
                if (existingAssignment is null)
                {
                    await _userRoleRepository.InsertAsync(new UserRole
                    {
                        UserId = userId,
                        RoleId = SystemRoles.AdministratorId,
                        CreatedUtc = DateTime.UtcNow
                    }, cancellationToken);
                }

                return;
            }

            if (existingAssignment is not null)
            {
                await _userRoleRepository.DeleteAsync(existingAssignment.Id, cancellationToken);
            }
        }

        private async Task EnsureMoreThanOneAdminAsync(Guid userIdBeingRemoved, CancellationToken cancellationToken)
        {
            var users = await _userRepository.GetAllAsync(cancellationToken);
            var adminCount = 0;

            foreach (var user in users)
            {
                if (await _userRoleRepository.UserHasRoleAsync(user.Id, SystemRoles.AdministratorId, cancellationToken))
                {
                    adminCount++;
                }
            }

            if (adminCount <= 1)
                throw new InvalidOperationException("The last administrator account cannot be removed or demoted.");
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

        private static string Normalize(string value)
        {
            return value.Trim().ToUpperInvariant();
        }

        private static string? NormalizeEmail(string? value)
        {
            var normalized = NormalizeNullableText(value);
            return normalized is null ? null : normalized.ToUpperInvariant();
        }

        private static string? NormalizeNullableText(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
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

        private static UserApiTokenDto MapUserToken(ApiToken token, string tenantName)
        {
            return new UserApiTokenDto
            {
                Id = token.Id,
                UserId = token.UserId,
                TenantId = token.TenantId ?? Guid.Empty,
                TenantName = tenantName,
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
