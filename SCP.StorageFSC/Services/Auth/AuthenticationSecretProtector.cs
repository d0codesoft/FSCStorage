using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;
using System.Runtime.Versioning;

namespace scp.filestorage.Services.Auth
{
    /// <summary>
    /// Protects authentication secrets on Windows and Linux.
    /// Windows uses DPAPI. Linux uses ASP.NET Core DataProtection.
    /// </summary>
    public sealed class AuthenticationSecretProtector : IAuthenticationSecretProtector
    {
        private const string WindowsPrefix = "win-dpapi-v1:";
        private const string LinuxPrefix = "linux-dp-v1:";

        private readonly IDataProtector _linuxProtector;

        public AuthenticationSecretProtector(
            IDataProtectionProvider dataProtectionProvider,
            IOptions<AuthenticationSecretProtectorOptions> options)
        {
            var protectorOptions = options.Value;

            if (string.IsNullOrWhiteSpace(protectorOptions.Purpose))
                throw new InvalidOperationException("Authentication secret protection purpose is not configured.");

            _linuxProtector = dataProtectionProvider.CreateProtector(protectorOptions.Purpose);
        }

        public string Protect(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Secret value cannot be empty.", nameof(value));

            if (OperatingSystem.IsWindows())
                return ProtectWindows(value);

            if (OperatingSystem.IsLinux())
                return ProtectLinux(value);

            throw new PlatformNotSupportedException("Authentication secret protection is supported only on Windows and Linux.");
        }

        public string Unprotect(string protectedValue)
        {
            if (string.IsNullOrWhiteSpace(protectedValue))
                throw new ArgumentException("Protected secret value cannot be empty.", nameof(protectedValue));

            if (protectedValue.StartsWith(WindowsPrefix, StringComparison.Ordinal))
            {
                if (!OperatingSystem.IsWindows())
                    throw new PlatformNotSupportedException("Windows DPAPI protected secrets can only be unprotected on Windows.");

                return UnprotectWindows(protectedValue);
            }

            if (protectedValue.StartsWith(LinuxPrefix, StringComparison.Ordinal))
                return UnprotectLinux(protectedValue);

            throw new CryptographicException("Unknown authentication secret protection format.");
        }

        [SupportedOSPlatform("windows")]
        private static string ProtectWindows(string value)
        {
            var plainBytes = Encoding.UTF8.GetBytes(value);

            var protectedBytes = ProtectedData.Protect(
                plainBytes,
                optionalEntropy: null,
                scope: DataProtectionScope.CurrentUser);

            CryptographicOperations.ZeroMemory(plainBytes);

            return WindowsPrefix + Convert.ToBase64String(protectedBytes);
        }

        [SupportedOSPlatform("windows")]
        private static string UnprotectWindows(string protectedValue)
        {
            var payload = protectedValue[WindowsPrefix.Length..];
            var protectedBytes = Convert.FromBase64String(payload);

            var plainBytes = ProtectedData.Unprotect(
                protectedBytes,
                optionalEntropy: null,
                scope: DataProtectionScope.CurrentUser);

            try
            {
                return Encoding.UTF8.GetString(plainBytes);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(plainBytes);
            }
        }

        private string ProtectLinux(string value)
        {
            var protectedText = _linuxProtector.Protect(value);
            return LinuxPrefix + protectedText;
        }

        private string UnprotectLinux(string protectedValue)
        {
            var payload = protectedValue[LinuxPrefix.Length..];
            return _linuxProtector.Unprotect(payload);
        }
    }
}
