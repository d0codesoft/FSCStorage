using scp.filestorage.Data.Models;

namespace scp.filestorage.Services.Auth
{
    public interface IAuthenticationService
    {
        Task<LoginResult> LoginAsync(
            LoginRequest request,
            CancellationToken cancellationToken = default);

        Task<VerifyTwoFactorResult> VerifyTwoFactorAsync(
            VerifyTwoFactorRequest request,
            CancellationToken cancellationToken = default);

        Task<VerifyTwoFactorResult> VerifyRecoveryCodeAsync(
            VerifyTwoFactorRequest request,
            CancellationToken cancellationToken = default);

        Task<AuthenticatorSetupResult> BeginEnableAuthenticatorAsync(
            Guid userId,
            string issuer,
            CancellationToken cancellationToken = default);

        Task<TwoFactorSetupStatus> ConfirmEnableAuthenticatorAsync(
            Guid userId,
            string code,
            CancellationToken cancellationToken = default);

        Task<bool> DisableTwoFactorAsync(
            Guid userId,
            CancellationToken cancellationToken = default);

        Task<bool> LockUserAsync(
            Guid userId,
            DateTime lockedUntilUtc,
            CancellationToken cancellationToken = default);

        Task<bool> UnlockUserAsync(
            Guid userId,
            CancellationToken cancellationToken = default);

        Task<bool> ChangePasswordAsync(
            Guid userId,
            string currentPassword,
            string newPassword,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<Role>> GetUserRolesAsync(
            Guid userId,
            CancellationToken cancellationToken = default);
    }
}
