using SCP.StorageFSC.Data.Dto;
using SCP.StorageFSC.Data.Models;
using SCP.StorageFSC.Data.Repositories;
using SCP.StorageFSC.InterfacesService;
using SCP.StorageFSC.Security;
using System.Security.Claims;
using scp.filestorage.Data.Models;
using scp.filestorage.Data.Repositories;
using scp.filestorage.Services.Auth;

namespace SCP.StorageFSC.Services
{
    public sealed class UserStorageService : IUserStorageService
    {
        private readonly ITenantRepository _tenantRepository;
        private readonly IApiTokenRepository _apiTokenRepository;
        private readonly IUserRepository _userRepository;
        private readonly IUserRoleRepository _userRoleRepository;
        private readonly IUserTwoFactorChallengeRepository _twoFactorChallengeRepository;
        private readonly ITenantFileRepository _tenantFileRepository;
        private readonly IStoredFileRepository _storedFileRepository;
        private readonly IDeletedTenantRepository _deletedTenantRepository;
        private readonly IPasswordHashService _passwordHashService;
        private readonly IUserTwoFactorMethodRepository _twoFactorMethodRepository;
        private readonly IUserRecoveryCodeRepository _recoveryCodeRepository;
        private readonly IAuthenticationHashService _authenticationHashService;
        private readonly IUserAuthenticationAuditService _userAuthenticationAuditService;
        private readonly IOneTimeCodeSender _oneTimeCodeSender;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<UserStorageService> _logger;

        public UserStorageService(
            ITenantRepository tenantRepository,
            IApiTokenRepository apiTokenRepository,
            IUserRepository userRepository,
            IUserRoleRepository userRoleRepository,
            IUserTwoFactorChallengeRepository twoFactorChallengeRepository,
            ITenantFileRepository tenantFileRepository,
            IStoredFileRepository storedFileRepository,
            IDeletedTenantRepository deletedTenantRepository,
            IPasswordHashService passwordHashService,
            IUserTwoFactorMethodRepository twoFactorMethodRepository,
            IUserRecoveryCodeRepository recoveryCodeRepository,
            IAuthenticationHashService authenticationHashService,
            IUserAuthenticationAuditService userAuthenticationAuditService,
            IOneTimeCodeSender oneTimeCodeSender,
            IHttpContextAccessor httpContextAccessor,
            ILogger<UserStorageService> logger)
        {
            _tenantRepository = tenantRepository;
            _apiTokenRepository = apiTokenRepository;
            _userRepository = userRepository;
            _userRoleRepository = userRoleRepository;
            _twoFactorChallengeRepository = twoFactorChallengeRepository;
            _tenantFileRepository = tenantFileRepository;
            _storedFileRepository = storedFileRepository;
            _deletedTenantRepository = deletedTenantRepository;
            _passwordHashService = passwordHashService;
            _twoFactorMethodRepository = twoFactorMethodRepository;
            _recoveryCodeRepository = recoveryCodeRepository;
            _authenticationHashService = authenticationHashService;
            _userAuthenticationAuditService = userAuthenticationAuditService;
            _oneTimeCodeSender = oneTimeCodeSender;
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
                    EmailConfirmed = user.EmailConfirmed,
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
                    EmailConfirmed = user.EmailConfirmed,
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
                await DisableEmailTwoFactorMethodAsync(user, cancellationToken);
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
            await DisableEmailTwoFactorMethodAsync(user, cancellationToken);
            user.MarkUpdated();

            return await _userRepository.UpdateAsync(user, cancellationToken);
        }

        public async Task<bool> SendUserEmailConfirmationAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            EnsureAdmin();

            var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
            if (user is null)
                return false;

            if (string.IsNullOrWhiteSpace(user.Email))
                throw new InvalidOperationException("User email is required before confirmation.");

            if (user.EmailConfirmed)
            {
                await EnsureEmailTwoFactorMethodAsync(user, cancellationToken);
                return true;
            }

            var code = CreateNumericCode(6);
            var challenge = new UserTwoFactorChallenge
            {
                UserId = user.Id,
                MethodType = TwoFactorMethodType.Email,
                CodeHash = _authenticationHashService.HashSecret(code),
                Destination = user.Email,
                Status = TwoFactorChallengeStatus.Pending,
                ExpiresUtc = DateTime.UtcNow.AddMinutes(10),
                CreatedIpAddress = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString(),
                UserAgent = _httpContextAccessor.HttpContext?.Request.Headers.UserAgent.ToString()
            };

            await _twoFactorChallengeRepository.InsertAsync(challenge, cancellationToken);
            await _oneTimeCodeSender.SendEmailCodeAsync(user.Email, code, cancellationToken);

            _logger.LogInformation(
                "Email confirmation code sent. UserId={UserId}, Email={MaskedEmail}",
                user.Id,
                MaskEmail(user.Email));

            return true;
        }

        public async Task<bool> ConfirmUserEmailAsync(Guid userId, string code, CancellationToken cancellationToken = default)
        {
            EnsureAdmin();

            if (string.IsNullOrWhiteSpace(code))
                throw new ArgumentException("Email confirmation code is required.", nameof(code));

            var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
            if (user is null)
                return false;

            if (string.IsNullOrWhiteSpace(user.Email))
                throw new InvalidOperationException("User email is required before confirmation.");

            var challenges = await _twoFactorChallengeRepository.GetPendingByUserIdAsync(user.Id, cancellationToken);
            var challenge = challenges
                .Where(item =>
                    item.MethodType == TwoFactorMethodType.Email &&
                    item.Status == TwoFactorChallengeStatus.Pending &&
                    string.Equals(item.Destination, user.Email, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(item => item.CreatedUtc)
                .FirstOrDefault();

            if (challenge is null)
                throw new InvalidOperationException("Email confirmation challenge was not found or has expired.");

            if (challenge.ExpiresUtc <= DateTime.UtcNow)
            {
                challenge.Status = TwoFactorChallengeStatus.Expired;
                challenge.MarkUpdated();
                await _twoFactorChallengeRepository.UpdateAsync(challenge, cancellationToken);
                throw new InvalidOperationException("Email confirmation code has expired.");
            }

            var codeHash = _authenticationHashService.HashSecret(code.Trim());
            if (!string.Equals(challenge.CodeHash, codeHash, StringComparison.Ordinal))
            {
                challenge.FailedAttemptCount++;
                if (challenge.FailedAttemptCount >= challenge.MaxFailedAttemptCount)
                {
                    challenge.Status = TwoFactorChallengeStatus.Blocked;
                }

                challenge.MarkUpdated();
                await _twoFactorChallengeRepository.UpdateAsync(challenge, cancellationToken);
                throw new InvalidOperationException("Email confirmation code is invalid.");
            }

            challenge.Status = TwoFactorChallengeStatus.Verified;
            challenge.VerifiedUtc = DateTime.UtcNow;
            challenge.VerifiedIpAddress = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
            challenge.MarkUpdated();
            await _twoFactorChallengeRepository.UpdateAsync(challenge, cancellationToken);

            user.EmailConfirmed = true;
            user.SecurityStamp = Guid.NewGuid().ToString("N");
            user.MarkUpdated();
            await _userRepository.UpdateAsync(user, cancellationToken);

            await EnsureEmailTwoFactorMethodAsync(user, cancellationToken);

            _logger.LogInformation(
                "Email confirmed. UserId={UserId}, Email={MaskedEmail}",
                user.Id,
                MaskEmail(user.Email));

            return true;
        }

        public async Task<bool> SetUserEmailConfirmedAsync(Guid userId, bool confirmed, CancellationToken cancellationToken = default)
        {
            EnsureAdmin();

            var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
            if (user is null)
                return false;

            user.EmailConfirmed = confirmed;
            if (!confirmed)
            {
                await DisableEmailTwoFactorMethodAsync(user, cancellationToken);
            }

            user.MarkUpdated();

            var updated = await _userRepository.UpdateAsync(user, cancellationToken);
            if (updated && confirmed)
            {
                await EnsureEmailTwoFactorMethodAsync(user, cancellationToken);
            }

            return updated;
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

            var methods = await _twoFactorMethodRepository.GetByUserIdAsync(user.Id, cancellationToken);
            if (!methods.Any(method => method.IsEnabled && method.IsConfirmed))
                throw new InvalidOperationException("At least one enabled and confirmed two-factor method is required.");

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

            var notifications = await _userAuthenticationAuditService.GetNotificationsAsync(
                userId,
                pageNumber: 1,
                pageSize: 100,
                cancellationToken);

            return notifications.Items
                .Select(item => new UserSecurityEventDto
                {
                    EventType = item.EventType,
                    OccurredUtc = item.CreatedUtc,
                    Description = BuildSecurityEventDescription(item)
                })
                .OrderByDescending(item => item.OccurredUtc)
                .ToList();
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

        private static string BuildSecurityEventDescription(UserAuthenticationNotificationDto notification)
        {
            var status = notification.IsSuccess ? "succeeded" : "failed";
            var reason = string.IsNullOrWhiteSpace(notification.FailureReason)
                ? string.Empty
                : $" Reason: {notification.FailureReason}.";

            return $"{notification.EventType} {status}. IP: {notification.ClientIp}.{reason}";
        }

        private static string CreateRecoveryCode()
        {
            Span<byte> bytes = stackalloc byte[9];
            System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }

        private static string CreateNumericCode(int digits)
        {
            if (digits <= 0)
                throw new ArgumentOutOfRangeException(nameof(digits));

            var min = (int)Math.Pow(10, digits - 1);
            var max = (int)Math.Pow(10, digits);
            return System.Security.Cryptography.RandomNumberGenerator.GetInt32(min, max).ToString();
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

        private static string ValidateUserName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("User name is required.", nameof(name));

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
                EmailConfirmed = user.EmailConfirmed,
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

        private async Task EnsureEmailTwoFactorMethodAsync(User user, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(user.Email) || !user.EmailConfirmed)
                return;

            var method = await _twoFactorMethodRepository.GetByUserAndTypeAsync(
                user.Id,
                TwoFactorMethodType.Email,
                cancellationToken);

            var now = DateTime.UtcNow;
            if (method is null)
            {
                await _twoFactorMethodRepository.InsertAsync(new UserTwoFactorMethod
                {
                    UserId = user.Id,
                    MethodType = TwoFactorMethodType.Email,
                    IsEnabled = true,
                    IsConfirmed = true,
                    IsDefault = false,
                    Destination = user.Email,
                    MaskedDestination = MaskEmail(user.Email),
                    ConfirmedUtc = now,
                    CreatedUtc = now
                }, cancellationToken);

                return;
            }

            method.IsEnabled = true;
            method.IsConfirmed = true;
            method.Destination = user.Email;
            method.MaskedDestination = MaskEmail(user.Email);
            method.ConfirmedUtc ??= now;
            method.MarkUpdated();

            await _twoFactorMethodRepository.UpdateAsync(method, cancellationToken);
        }

        private async Task DisableEmailTwoFactorMethodAsync(User user, CancellationToken cancellationToken)
        {
            var method = await _twoFactorMethodRepository.GetByUserAndTypeAsync(
                user.Id,
                TwoFactorMethodType.Email,
                cancellationToken);

            if (method is null)
                return;

            method.IsEnabled = false;
            method.IsDefault = false;
            method.MarkUpdated();
            await _twoFactorMethodRepository.UpdateAsync(method, cancellationToken);

            var methods = await _twoFactorMethodRepository.GetByUserIdAsync(user.Id, cancellationToken);
            if (methods.Any(item => item.Id != method.Id && item.IsEnabled && item.IsConfirmed))
                return;

            user.TwoFactorEnabled = false;
            user.TwoFactorEnabledUtc = null;
            user.TwoFactorLastUsedUtc = null;
            user.PreferredTwoFactorMethod = TwoFactorMethodType.None;
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

        private static string MaskEmail(string email)
        {
            var atIndex = email.IndexOf('@', StringComparison.Ordinal);
            if (atIndex <= 1)
                return "***";

            var name = email[..atIndex];
            var domain = email[(atIndex + 1)..];
            var visibleName = name.Length <= 2 ? name[0].ToString() : name[..2];

            return $"{visibleName}***@{domain}";
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
