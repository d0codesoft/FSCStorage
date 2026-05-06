using scp.filestorage.Data.Models;
using scp.filestorage.Data.Repositories;
using SCP.StorageFSC.Data.Models;
using System.Security.Cryptography;

namespace scp.filestorage.Services.Auth
{
    public sealed class AuthenticationService : IAuthenticationService
    {
        private const int MaxFailedPasswordAttempts = 5;
        private static readonly TimeSpan UserLockoutDuration = TimeSpan.FromMinutes(15);
        private static readonly TimeSpan LoginChallengeLifetime = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan TwoFactorCodeLifetime = TimeSpan.FromMinutes(5);

        private readonly IUserRepository _userRepository;
        private readonly IUserTwoFactorMethodRepository _twoFactorMethodRepository;
        private readonly IUserTwoFactorChallengeRepository _twoFactorChallengeRepository;
        private readonly IUserLoginChallengeRepository _loginChallengeRepository;
        private readonly IUserRecoveryCodeRepository _recoveryCodeRepository;
        private readonly IUserRoleRepository _userRoleRepository;
        private readonly IPasswordHashService _passwordHashService;
        private readonly IAuthenticationHashService _hashService;
        private readonly IAuthenticationSecretProtector _secretProtector;
        private readonly ITotpService _totpService;
        private readonly IOneTimeCodeSender _oneTimeCodeSender;

        public AuthenticationService(
            IUserRepository userRepository,
            IUserTwoFactorMethodRepository twoFactorMethodRepository,
            IUserTwoFactorChallengeRepository twoFactorChallengeRepository,
            IUserLoginChallengeRepository loginChallengeRepository,
            IUserRecoveryCodeRepository recoveryCodeRepository,
            IUserRoleRepository userRoleRepository,
            IPasswordHashService passwordHashService,
            IAuthenticationHashService hashService,
            IAuthenticationSecretProtector secretProtector,
            ITotpService totpService,
            IOneTimeCodeSender oneTimeCodeSender)
        {
            _userRepository = userRepository;
            _twoFactorMethodRepository = twoFactorMethodRepository;
            _twoFactorChallengeRepository = twoFactorChallengeRepository;
            _loginChallengeRepository = loginChallengeRepository;
            _recoveryCodeRepository = recoveryCodeRepository;
            _userRoleRepository = userRoleRepository;
            _passwordHashService = passwordHashService;
            _hashService = hashService;
            _secretProtector = secretProtector;
            _totpService = totpService;
            _oneTimeCodeSender = oneTimeCodeSender;
        }

        public async Task<LoginResult> LoginAsync(
            LoginRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            var login = request.Login.Trim();

            if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(request.Password))
            {
                return new LoginResult
                {
                    Status = AuthLoginStatus.InvalidCredentials
                };
            }

            var user = await FindUserByLoginAsync(login, cancellationToken);

            if (user is null)
            {
                return new LoginResult
                {
                    Status = AuthLoginStatus.UserNotFound
                };
            }

            var validationStatus = ValidateUserState(user);

            if (validationStatus != AuthLoginStatus.Success)
            {
                return new LoginResult
                {
                    Status = validationStatus,
                    UserId = user.Id
                };
            }

            var passwordValid = _passwordHashService.VerifyPassword(user, request.Password);

            if (!passwordValid)
            {
                await RegisterFailedPasswordAttemptAsync(user, cancellationToken);

                return new LoginResult
                {
                    Status = AuthLoginStatus.InvalidCredentials,
                    UserId = user.Id
                };
            }

            await RegisterSuccessfulPasswordAttemptAsync(user, request.IpAddress, cancellationToken);

            if (user.MustChangePassword)
            {
                return new LoginResult
                {
                    Status = AuthLoginStatus.PasswordChangeRequired,
                    UserId = user.Id
                };
            }

            if (user.PasswordExpiresUtc is not null && user.PasswordExpiresUtc <= DateTime.UtcNow)
            {
                return new LoginResult
                {
                    Status = AuthLoginStatus.PasswordExpired,
                    UserId = user.Id
                };
            }

            if (user.TwoFactorEnabled && user.TwoFactorRequiredForEveryLogin)
            {
                var twoFactorMethod = await ResolveTwoFactorMethodAsync(user, cancellationToken);

                if (twoFactorMethod is null)
                {
                    return new LoginResult
                    {
                        Status = AuthLoginStatus.InvalidCredentials,
                        UserId = user.Id
                    };
                }

                if (twoFactorMethod.MethodType is TwoFactorMethodType.AuthenticatorApp &&
                    !string.IsNullOrWhiteSpace(request.TwoFactorCode))
                {
                    var authenticatorVerified = await VerifyAuthenticatorCodeAsync(
                        user.Id,
                        request.TwoFactorCode,
                        cancellationToken);

                    if (authenticatorVerified)
                    {
                        user.TwoFactorLastUsedUtc = DateTime.UtcNow;
                        user.LastLoginUtc = DateTime.UtcNow;
                        user.LastLoginIpAddress = request.IpAddress;
                        user.MarkUpdated();

                        await _userRepository.UpdateAsync(user, cancellationToken);

                        var authenticatedRoles = await GetUserRoleNamesAsync(user.Id, cancellationToken);

                        return new LoginResult
                        {
                            Status = AuthLoginStatus.Success,
                            UserId = user.Id,
                            Roles = authenticatedRoles
                        };
                    }
                }

                var plainChallengeToken = CreateSecureToken();
                var challengeTokenHash = _hashService.HashSecret(plainChallengeToken);
                var expiresUtc = DateTime.UtcNow.Add(LoginChallengeLifetime);

                Guid? twoFactorChallengeId = null;

                if (twoFactorMethod.MethodType is TwoFactorMethodType.Email or TwoFactorMethodType.Sms)
                {
                    var code = CreateNumericCode(6);
                    var codeHash = _hashService.HashSecret(code);

                    var destination = GetTwoFactorDestination(user, twoFactorMethod);

                    var challenge = new UserTwoFactorChallenge
                    {
                        UserId = user.Id,
                        UserTwoFactorMethodId = twoFactorMethod.Id,
                        MethodType = twoFactorMethod.MethodType,
                        CodeHash = codeHash,
                        Destination = destination,
                        Status = TwoFactorChallengeStatus.Pending,
                        ExpiresUtc = DateTime.UtcNow.Add(TwoFactorCodeLifetime),
                        CreatedIpAddress = request.IpAddress,
                        UserAgent = request.UserAgent
                    };

                    await _twoFactorChallengeRepository.InsertAsync(challenge, cancellationToken);

                    twoFactorChallengeId = challenge.Id;

                    if (twoFactorMethod.MethodType == TwoFactorMethodType.Email)
                    {
                        await _oneTimeCodeSender.SendEmailCodeAsync(destination, code, cancellationToken);
                    }
                    else
                    {
                        await _oneTimeCodeSender.SendSmsCodeAsync(destination, code, cancellationToken);
                    }
                }

                var loginChallenge = new UserLoginChallenge
                {
                    UserId = user.Id,
                    ChallengeTokenHash = challengeTokenHash,
                    MethodType = twoFactorMethod.MethodType,
                    TwoFactorChallengeId = twoFactorChallengeId,
                    Status = UserLoginChallengeStatus.Pending,
                    ExpiresUtc = expiresUtc,
                    IpAddress = request.IpAddress,
                    UserAgent = request.UserAgent
                };

                await _loginChallengeRepository.InsertAsync(loginChallenge, cancellationToken);

                return new LoginResult
                {
                    Status = AuthLoginStatus.TwoFactorRequired,
                    UserId = user.Id,
                    RequiresTwoFactor = true,
                    TwoFactorMethod = twoFactorMethod.MethodType,
                    ChallengeToken = plainChallengeToken,
                    ChallengeExpiresUtc = expiresUtc
                };
            }

            var roles = await GetUserRoleNamesAsync(user.Id, cancellationToken);

            return new LoginResult
            {
                Status = AuthLoginStatus.Success,
                UserId = user.Id,
                Roles = roles
            };
        }

        public async Task<VerifyTwoFactorResult> VerifyTwoFactorAsync(
            VerifyTwoFactorRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (string.IsNullOrWhiteSpace(request.ChallengeToken) || string.IsNullOrWhiteSpace(request.Code))
            {
                return new VerifyTwoFactorResult
                {
                    Status = TwoFactorVerifyStatus.InvalidChallenge
                };
            }

            var challengeTokenHash = _hashService.HashSecret(request.ChallengeToken);

            var loginChallenge = await _loginChallengeRepository.GetByTokenHashAsync(
                challengeTokenHash,
                cancellationToken);

            if (loginChallenge is null || loginChallenge.Status != UserLoginChallengeStatus.Pending)
            {
                return new VerifyTwoFactorResult
                {
                    Status = TwoFactorVerifyStatus.InvalidChallenge
                };
            }

            if (loginChallenge.ExpiresUtc <= DateTime.UtcNow)
            {
                loginChallenge.Status = UserLoginChallengeStatus.Expired;
                loginChallenge.MarkUpdated();

                await _loginChallengeRepository.UpdateAsync(loginChallenge, cancellationToken);

                return new VerifyTwoFactorResult
                {
                    Status = TwoFactorVerifyStatus.ChallengeExpired,
                    UserId = loginChallenge.UserId
                };
            }

            if (loginChallenge.FailedAttemptCount >= loginChallenge.MaxFailedAttemptCount)
            {
                loginChallenge.Status = UserLoginChallengeStatus.Blocked;
                loginChallenge.MarkUpdated();

                await _loginChallengeRepository.UpdateAsync(loginChallenge, cancellationToken);

                return new VerifyTwoFactorResult
                {
                    Status = TwoFactorVerifyStatus.ChallengeBlocked,
                    UserId = loginChallenge.UserId
                };
            }

            var user = await _userRepository.GetByIdAsync(loginChallenge.UserId, cancellationToken);

            if (user is null)
            {
                return new VerifyTwoFactorResult
                {
                    Status = TwoFactorVerifyStatus.InvalidChallenge
                };
            }

            var userStatus = ValidateUserForTwoFactor(user);

            if (userStatus != TwoFactorVerifyStatus.Success)
            {
                return new VerifyTwoFactorResult
                {
                    Status = userStatus,
                    UserId = user.Id
                };
            }

            var verified = loginChallenge.MethodType switch
            {
                TwoFactorMethodType.AuthenticatorApp => await VerifyAuthenticatorCodeAsync(
                    user.Id,
                    request.Code,
                    cancellationToken),

                TwoFactorMethodType.Email or TwoFactorMethodType.Sms => await VerifyEmailOrSmsCodeAsync(
                    loginChallenge,
                    request.Code,
                    request.IpAddress,
                    cancellationToken),

                _ => false
            };

            if (!verified)
            {
                loginChallenge.FailedAttemptCount++;

                if (loginChallenge.FailedAttemptCount >= loginChallenge.MaxFailedAttemptCount)
                {
                    loginChallenge.Status = UserLoginChallengeStatus.Blocked;
                }

                loginChallenge.MarkUpdated();

                await _loginChallengeRepository.UpdateAsync(loginChallenge, cancellationToken);

                return new VerifyTwoFactorResult
                {
                    Status = TwoFactorVerifyStatus.InvalidCode,
                    UserId = user.Id
                };
            }

            loginChallenge.Status = UserLoginChallengeStatus.Completed;
            loginChallenge.CompletedUtc = DateTime.UtcNow;
            loginChallenge.MarkUpdated();

            await _loginChallengeRepository.UpdateAsync(loginChallenge, cancellationToken);

            user.TwoFactorLastUsedUtc = DateTime.UtcNow;
            user.LastLoginUtc = DateTime.UtcNow;
            user.LastLoginIpAddress = request.IpAddress;
            user.MarkUpdated();

            await _userRepository.UpdateAsync(user, cancellationToken);

            var roles = await GetUserRoleNamesAsync(user.Id, cancellationToken);

            return new VerifyTwoFactorResult
            {
                Status = TwoFactorVerifyStatus.Success,
                UserId = user.Id,
                Roles = roles
            };
        }

        public async Task<VerifyTwoFactorResult> VerifyRecoveryCodeAsync(
            VerifyTwoFactorRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            var challengeTokenHash = _hashService.HashSecret(request.ChallengeToken);

            var loginChallenge = await _loginChallengeRepository.GetByTokenHashAsync(
                challengeTokenHash,
                cancellationToken);

            if (loginChallenge is null || loginChallenge.Status != UserLoginChallengeStatus.Pending)
            {
                return new VerifyTwoFactorResult
                {
                    Status = TwoFactorVerifyStatus.InvalidChallenge
                };
            }

            if (loginChallenge.ExpiresUtc <= DateTime.UtcNow)
            {
                loginChallenge.Status = UserLoginChallengeStatus.Expired;
                loginChallenge.MarkUpdated();

                await _loginChallengeRepository.UpdateAsync(loginChallenge, cancellationToken);

                return new VerifyTwoFactorResult
                {
                    Status = TwoFactorVerifyStatus.ChallengeExpired,
                    UserId = loginChallenge.UserId
                };
            }

            var user = await _userRepository.GetByIdAsync(loginChallenge.UserId, cancellationToken);

            if (user is null)
            {
                return new VerifyTwoFactorResult
                {
                    Status = TwoFactorVerifyStatus.InvalidChallenge
                };
            }

            var codeHash = _hashService.HashSecret(request.Code);

            var recoveryCode = await _recoveryCodeRepository.GetUnusedByHashAsync(
                user.Id,
                codeHash,
                cancellationToken);

            if (recoveryCode is null)
            {
                loginChallenge.FailedAttemptCount++;

                if (loginChallenge.FailedAttemptCount >= loginChallenge.MaxFailedAttemptCount)
                {
                    loginChallenge.Status = UserLoginChallengeStatus.Blocked;
                }

                loginChallenge.MarkUpdated();

                await _loginChallengeRepository.UpdateAsync(loginChallenge, cancellationToken);

                return new VerifyTwoFactorResult
                {
                    Status = TwoFactorVerifyStatus.InvalidCode,
                    UserId = user.Id
                };
            }

            await _recoveryCodeRepository.MarkUsedAsync(
                recoveryCode.Id,
                DateTime.UtcNow,
                request.IpAddress,
                request.UserAgent,
                cancellationToken);

            loginChallenge.Status = UserLoginChallengeStatus.Completed;
            loginChallenge.CompletedUtc = DateTime.UtcNow;
            loginChallenge.MarkUpdated();

            await _loginChallengeRepository.UpdateAsync(loginChallenge, cancellationToken);

            user.TwoFactorLastUsedUtc = DateTime.UtcNow;
            user.LastLoginUtc = DateTime.UtcNow;
            user.LastLoginIpAddress = request.IpAddress;
            user.MarkUpdated();

            await _userRepository.UpdateAsync(user, cancellationToken);

            var roles = await GetUserRoleNamesAsync(user.Id, cancellationToken);

            return new VerifyTwoFactorResult
            {
                Status = TwoFactorVerifyStatus.Success,
                UserId = user.Id,
                Roles = roles
            };
        }

        public async Task<AuthenticatorSetupResult> BeginEnableAuthenticatorAsync(
            Guid userId,
            string issuer,
            CancellationToken cancellationToken = default)
        {
            var user = await _userRepository.GetByIdAsync(userId, cancellationToken);

            if (user is null)
            {
                return new AuthenticatorSetupResult
                {
                    Status = TwoFactorSetupStatus.UserNotFound
                };
            }

            if (!user.IsActive)
            {
                return new AuthenticatorSetupResult
                {
                    Status = TwoFactorSetupStatus.UserInactive
                };
            }

            var existingMethod = await _twoFactorMethodRepository.GetByUserAndTypeAsync(
                user.Id,
                TwoFactorMethodType.AuthenticatorApp,
                cancellationToken);

            if (existingMethod is not null && existingMethod.IsEnabled && existingMethod.IsConfirmed)
            {
                return new AuthenticatorSetupResult
                {
                    Status = TwoFactorSetupStatus.MethodAlreadyExists
                };
            }

            var secret = _totpService.GenerateSecret();
            var protectedSecret = _secretProtector.Protect(secret);

            if (existingMethod is null)
            {
                var method = new UserTwoFactorMethod
                {
                    UserId = user.Id,
                    MethodType = TwoFactorMethodType.AuthenticatorApp,
                    IsEnabled = false,
                    IsConfirmed = false,
                    IsDefault = true,
                    SecretEncrypted = protectedSecret
                };

                await _twoFactorMethodRepository.InsertAsync(method, cancellationToken);
            }
            else
            {
                existingMethod.IsEnabled = false;
                existingMethod.IsConfirmed = false;
                existingMethod.IsDefault = true;
                existingMethod.SecretEncrypted = protectedSecret;
                existingMethod.ConfirmedUtc = null;
                existingMethod.MarkUpdated();

                await _twoFactorMethodRepository.UpdateAsync(existingMethod, cancellationToken);
            }

            var accountName = user.Email ?? user.Name;
            var otpAuthUri = _totpService.CreateOtpAuthUri(issuer, accountName, secret);

            return new AuthenticatorSetupResult
            {
                Status = TwoFactorSetupStatus.Success,
                Secret = secret,
                OtpAuthUri = otpAuthUri
            };
        }

        public async Task<TwoFactorSetupStatus> ConfirmEnableAuthenticatorAsync(
            Guid userId,
            string code,
            CancellationToken cancellationToken = default)
        {
            var user = await _userRepository.GetByIdAsync(userId, cancellationToken);

            if (user is null)
                return TwoFactorSetupStatus.UserNotFound;

            if (!user.IsActive)
                return TwoFactorSetupStatus.UserInactive;

            var method = await _twoFactorMethodRepository.GetByUserAndTypeAsync(
                user.Id,
                TwoFactorMethodType.AuthenticatorApp,
                cancellationToken);

            if (method is null || string.IsNullOrWhiteSpace(method.SecretEncrypted))
                return TwoFactorSetupStatus.MethodAlreadyExists;

            var secret = _secretProtector.Unprotect(method.SecretEncrypted);

            if (!_totpService.VerifyCode(secret, code))
                return TwoFactorSetupStatus.InvalidCode;

            method.IsEnabled = true;
            method.IsConfirmed = true;
            method.IsDefault = true;
            method.ConfirmedUtc = DateTime.UtcNow;
            method.FailedAttemptCount = 0;
            method.LockedUntilUtc = null;
            method.MarkUpdated();

            await _twoFactorMethodRepository.UpdateAsync(method, cancellationToken);

            user.TwoFactorEnabled = true;
            user.TwoFactorRequiredForEveryLogin = true;
            user.PreferredTwoFactorMethod = TwoFactorMethodType.AuthenticatorApp;
            user.TwoFactorEnabledUtc = DateTime.UtcNow;
            user.SecurityStamp = Guid.NewGuid().ToString("N");
            user.MarkUpdated();

            await _userRepository.UpdateAsync(user, cancellationToken);

            return TwoFactorSetupStatus.Success;
        }

        public async Task<bool> DisableTwoFactorAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
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
            user.TwoFactorLastUsedUtc = null;
            user.SecurityStamp = Guid.NewGuid().ToString("N");
            user.MarkUpdated();

            return await _userRepository.UpdateAsync(user, cancellationToken);
        }

        public async Task<bool> LockUserAsync(
            Guid userId,
            DateTime lockedUntilUtc,
            CancellationToken cancellationToken = default)
        {
            var user = await _userRepository.GetByIdAsync(userId, cancellationToken);

            if (user is null)
                return false;

            user.IsLocked = true;
            user.LockedUntilUtc = lockedUntilUtc;
            user.SecurityStamp = Guid.NewGuid().ToString("N");
            user.MarkUpdated();

            return await _userRepository.UpdateAsync(user, cancellationToken);
        }

        public async Task<bool> UnlockUserAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
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

        public async Task<bool> ChangePasswordAsync(
            Guid userId,
            string currentPassword,
            string newPassword,
            CancellationToken cancellationToken = default)
        {
            var user = await _userRepository.GetByIdAsync(userId, cancellationToken);

            if (user is null || !user.IsActive)
                return false;

            if (!_passwordHashService.VerifyPassword(user, currentPassword))
                return false;

            user.PasswordHash = _passwordHashService.HashPassword(user, newPassword);
            user.PasswordChangedUtc = DateTime.UtcNow;
            user.MustChangePassword = false;
            user.SecurityStamp = Guid.NewGuid().ToString("N");
            user.MarkUpdated();

            return await _userRepository.UpdateAsync(user, cancellationToken);
        }

        public Task<IReadOnlyList<Role>> GetUserRolesAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            return _userRoleRepository.GetRolesByUserIdAsync(userId, cancellationToken);
        }

        private async Task<User?> FindUserByLoginAsync(
            string login,
            CancellationToken cancellationToken)
        {
            var normalized = Normalize(login);

            if (login.Contains('@', StringComparison.Ordinal))
            {
                return await _userRepository.GetByNormalizedEmailAsync(
                    normalized,
                    cancellationToken);
            }

            return await _userRepository.GetByNormalizedNameAsync(
                normalized,
                cancellationToken);
        }

        private static AuthLoginStatus ValidateUserState(User user)
        {
            if (!user.IsActive)
                return AuthLoginStatus.UserInactive;

            if (user.IsLocked)
            {
                if (user.LockedUntilUtc is null)
                    return AuthLoginStatus.UserLocked;

                if (user.LockedUntilUtc > DateTime.UtcNow)
                    return AuthLoginStatus.UserLocked;
            }

            return AuthLoginStatus.Success;
        }

        private static TwoFactorVerifyStatus ValidateUserForTwoFactor(User user)
        {
            if (!user.IsActive)
                return TwoFactorVerifyStatus.UserInactive;

            if (user.IsLocked && (user.LockedUntilUtc is null || user.LockedUntilUtc > DateTime.UtcNow))
                return TwoFactorVerifyStatus.UserLocked;

            return TwoFactorVerifyStatus.Success;
        }

        private async Task RegisterFailedPasswordAttemptAsync(
            User user,
            CancellationToken cancellationToken)
        {
            user.FailedLoginCount++;
            user.LastFailedLoginUtc = DateTime.UtcNow;

            if (user.FailedLoginCount >= MaxFailedPasswordAttempts)
            {
                user.IsLocked = true;
                user.LockedUntilUtc = DateTime.UtcNow.Add(UserLockoutDuration);
            }

            user.MarkUpdated();

            await _userRepository.UpdateAsync(user, cancellationToken);
        }

        private async Task RegisterSuccessfulPasswordAttemptAsync(
            User user,
            string? ipAddress,
            CancellationToken cancellationToken)
        {
            user.FailedLoginCount = 0;
            user.LastFailedLoginUtc = null;

            if (user.IsLocked && user.LockedUntilUtc is not null && user.LockedUntilUtc <= DateTime.UtcNow)
            {
                user.IsLocked = false;
                user.LockedUntilUtc = null;
            }

            if (!user.TwoFactorEnabled || !user.TwoFactorRequiredForEveryLogin)
            {
                user.LastLoginUtc = DateTime.UtcNow;
                user.LastLoginIpAddress = ipAddress;
            }

            user.MarkUpdated();

            await _userRepository.UpdateAsync(user, cancellationToken);
        }

        private async Task<UserTwoFactorMethod?> ResolveTwoFactorMethodAsync(
            User user,
            CancellationToken cancellationToken)
        {
            var defaultMethod = await _twoFactorMethodRepository.GetDefaultAsync(
                user.Id,
                cancellationToken);

            if (IsMethodUsable(defaultMethod))
                return defaultMethod;

            var preferredMethod = await _twoFactorMethodRepository.GetByUserAndTypeAsync(
                user.Id,
                user.PreferredTwoFactorMethod,
                cancellationToken);

            if (IsMethodUsable(preferredMethod))
                return preferredMethod;

            var methods = await _twoFactorMethodRepository.GetByUserIdAsync(
                user.Id,
                cancellationToken);

            return methods.FirstOrDefault(IsMethodUsable);
        }

        private static bool IsMethodUsable(UserTwoFactorMethod? method)
        {
            if (method is null)
                return false;

            if (!method.IsEnabled || !method.IsConfirmed)
                return false;

            if (method.LockedUntilUtc is not null && method.LockedUntilUtc > DateTime.UtcNow)
                return false;

            return true;
        }

        private async Task<bool> VerifyAuthenticatorCodeAsync(
            Guid userId,
            string code,
            CancellationToken cancellationToken)
        {
            var method = await _twoFactorMethodRepository.GetByUserAndTypeAsync(
                userId,
                TwoFactorMethodType.AuthenticatorApp,
                cancellationToken);

            if (!IsMethodUsable(method) || string.IsNullOrWhiteSpace(method!.SecretEncrypted))
                return false;

            var secret = _secretProtector.Unprotect(method.SecretEncrypted);

            var verified = _totpService.VerifyCode(secret, code);

            if (verified)
            {
                method.FailedAttemptCount = 0;
                method.LastFailedAttemptUtc = null;
                method.LastUsedUtc = DateTime.UtcNow;
                method.MarkUpdated();

                await _twoFactorMethodRepository.UpdateAsync(method, cancellationToken);

                return true;
            }

            method.FailedAttemptCount++;
            method.LastFailedAttemptUtc = DateTime.UtcNow;

            if (method.FailedAttemptCount >= 5)
            {
                method.LockedUntilUtc = DateTime.UtcNow.AddMinutes(15);
            }

            method.MarkUpdated();

            await _twoFactorMethodRepository.UpdateAsync(method, cancellationToken);

            return false;
        }

        private async Task<bool> VerifyEmailOrSmsCodeAsync(
            UserLoginChallenge loginChallenge,
            string code,
            string? verifiedIpAddress,
            CancellationToken cancellationToken)
        {
            if (loginChallenge.TwoFactorChallengeId is null)
                return false;

            var challenge = await _twoFactorChallengeRepository.GetByIdAsync(
                loginChallenge.TwoFactorChallengeId.Value,
                cancellationToken);

            if (challenge is null || challenge.Status != TwoFactorChallengeStatus.Pending)
                return false;

            if (challenge.ExpiresUtc <= DateTime.UtcNow)
            {
                challenge.Status = TwoFactorChallengeStatus.Expired;
                challenge.MarkUpdated();

                await _twoFactorChallengeRepository.UpdateAsync(challenge, cancellationToken);

                return false;
            }

            if (challenge.FailedAttemptCount >= challenge.MaxFailedAttemptCount)
            {
                challenge.Status = TwoFactorChallengeStatus.Blocked;
                challenge.MarkUpdated();

                await _twoFactorChallengeRepository.UpdateAsync(challenge, cancellationToken);

                return false;
            }

            var codeHash = _hashService.HashSecret(code);

            if (!CryptographicOperations.FixedTimeEquals(
                    Convert.FromHexString(challenge.CodeHash),
                    Convert.FromHexString(codeHash)))
            {
                challenge.FailedAttemptCount++;

                if (challenge.FailedAttemptCount >= challenge.MaxFailedAttemptCount)
                {
                    challenge.Status = TwoFactorChallengeStatus.Blocked;
                }

                challenge.MarkUpdated();

                await _twoFactorChallengeRepository.UpdateAsync(challenge, cancellationToken);

                return false;
            }

            challenge.Status = TwoFactorChallengeStatus.Verified;
            challenge.VerifiedUtc = DateTime.UtcNow;
            challenge.VerifiedIpAddress = verifiedIpAddress;
            challenge.MarkUpdated();

            await _twoFactorChallengeRepository.UpdateAsync(challenge, cancellationToken);

            return true;
        }

        private static string GetTwoFactorDestination(
            User user,
            UserTwoFactorMethod method)
        {
            if (!string.IsNullOrWhiteSpace(method.Destination))
                return method.Destination;

            return method.MethodType switch
            {
                TwoFactorMethodType.Email when !string.IsNullOrWhiteSpace(user.Email) => user.Email,
                TwoFactorMethodType.Sms when !string.IsNullOrWhiteSpace(user.PhoneNumber) => user.PhoneNumber,
                _ => throw new InvalidOperationException("Two-factor destination is not configured.")
            };
        }

        private async Task<IReadOnlyList<string>> GetUserRoleNamesAsync(
            Guid userId,
            CancellationToken cancellationToken)
        {
            var roles = await _userRoleRepository.GetRolesByUserIdAsync(
                userId,
                cancellationToken);

            return roles
                .Select(x => x.Name)
                .ToArray();
        }

        private static string Normalize(string value)
        {
            return value.Trim().ToUpperInvariant();
        }

        private static string CreateSecureToken()
        {
            Span<byte> bytes = stackalloc byte[32];
            RandomNumberGenerator.Fill(bytes);
            return Convert.ToBase64String(bytes);
        }

        private static string CreateNumericCode(int length)
        {
            if (length <= 0)
                throw new ArgumentOutOfRangeException(nameof(length));

            var chars = new char[length];

            for (var i = 0; i < chars.Length; i++)
            {
                chars[i] = (char)('0' + RandomNumberGenerator.GetInt32(0, 10));
            }

            return new string(chars);
        }
    }
}
