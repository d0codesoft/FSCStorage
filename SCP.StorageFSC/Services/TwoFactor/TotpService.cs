using Microsoft.Extensions.Options;
using OtpNet;
using scp.filestorage.Services.Auth;
using System.Security.Cryptography;
using System.Text;

namespace scp.filestorage.Services.TwoFactor
{
    /// <summary>
    /// Provides TOTP functionality compatible with Google Authenticator,
    /// Microsoft Authenticator, Authy, and similar applications.
    /// </summary>
    public sealed class TotpService : ITotpService
    {
        private readonly TotpOptions _options;

        public TotpService(IOptions<TotpOptions> options)
        {
            _options = options.Value;
        }

        public string GenerateSecret()
        {
            if (_options.SecretSizeBytes < 16)
                throw new InvalidOperationException("TOTP secret size must be at least 16 bytes.");

            var secretBytes = RandomNumberGenerator.GetBytes(_options.SecretSizeBytes);

            return Base32Encoding.ToString(secretBytes);
        }

        public bool VerifyCode(string secret, string code)
        {
            if (string.IsNullOrWhiteSpace(secret))
                return false;

            if (string.IsNullOrWhiteSpace(code))
                return false;

            code = NormalizeCode(code);

            if (code.Length != _options.CodeDigits)
                return false;

            if (!code.All(char.IsDigit))
                return false;

            byte[] secretBytes;

            try
            {
                secretBytes = Base32Encoding.ToBytes(NormalizeSecret(secret));
            }
            catch
            {
                return false;
            }

            var totp = new Totp(
                secretBytes,
                step: _options.StepSeconds,
                totpSize: _options.CodeDigits,
                mode: OtpHashMode.Sha1);

            var verificationWindow = new VerificationWindow(
                previous: _options.VerificationWindowSteps,
                future: _options.VerificationWindowSteps);

            return totp.VerifyTotp(
                code,
                out _,
                verificationWindow);
        }

        public string CreateOtpAuthUri(string issuer, string accountName, string secret)
        {
            if (string.IsNullOrWhiteSpace(issuer))
                throw new ArgumentException("Issuer cannot be empty.", nameof(issuer));

            if (string.IsNullOrWhiteSpace(accountName))
                throw new ArgumentException("Account name cannot be empty.", nameof(accountName));

            if (string.IsNullOrWhiteSpace(secret))
                throw new ArgumentException("Secret cannot be empty.", nameof(secret));

            var normalizedSecret = NormalizeSecret(secret);

            var label = Uri.EscapeDataString($"{issuer}:{accountName}");
            var issuerValue = Uri.EscapeDataString(issuer);

            return
                $"otpauth://totp/{label}" +
                $"?secret={normalizedSecret}" +
                $"&issuer={issuerValue}" +
                $"&algorithm=SHA1" +
                $"&digits={_options.CodeDigits}" +
                $"&period={_options.StepSeconds}";
        }

        private static string NormalizeSecret(string secret)
        {
            return secret
                .Trim()
                .Replace(" ", string.Empty, StringComparison.Ordinal)
                .Replace("-", string.Empty, StringComparison.Ordinal)
                .ToUpperInvariant();
        }

        private static string NormalizeCode(string code)
        {
            var builder = new StringBuilder(code.Length);

            foreach (var ch in code)
            {
                if (!char.IsWhiteSpace(ch))
                    builder.Append(ch);
            }

            return builder.ToString();
        }
    }
}
