using System.Security.Cryptography;
using System.Text;

namespace scp.filestorage.Services.Auth
{
    public sealed class AuthenticationHashService : IAuthenticationHashService
    {
        public string HashSecret(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Value cannot be empty.", nameof(value));

            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
            return Convert.ToHexString(bytes);
        }
    }
}
