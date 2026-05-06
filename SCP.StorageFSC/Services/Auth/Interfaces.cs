using SCP.StorageFSC.Data.Models;

namespace scp.filestorage.Services.Auth
{
    /// <summary>
    /// Provides password hashing and verification.
    /// </summary>
    public interface IPasswordHashService
    {
        string HashPassword(User user, string password);

        bool VerifyPassword(User user, string password);
    }

    /// <summary>
    /// Provides hashing for challenge tokens, 2FA codes, and recovery codes.
    /// </summary>
    public interface IAuthenticationHashService
    {
        string HashSecret(string value);
    }

    /// <summary>
    /// Provides encryption for persisted authentication secrets.
    /// </summary>
    public interface IAuthenticationSecretProtector
    {
        string Protect(string value);

        string Unprotect(string protectedValue);
    }

    /// <summary>
    /// Provides TOTP secret generation and validation.
    /// </summary>
    public interface ITotpService
    {
        string GenerateSecret();

        bool VerifyCode(string secret, string code);

        string CreateOtpAuthUri(string issuer, string accountName, string secret);
    }

    /// <summary>
    /// Sends one-time authentication codes to users.
    /// </summary>
    public interface IOneTimeCodeSender
    {
        Task SendEmailCodeAsync(string email, string code, CancellationToken cancellationToken = default);

        Task SendSmsCodeAsync(string phoneNumber, string code, CancellationToken cancellationToken = default);
    }
}
