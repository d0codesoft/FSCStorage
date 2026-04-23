using scp.filestorage.Security;
using System.Security.Cryptography;
using System.Text;

namespace SCP.StorageFSC.Security
{
    public static class TokenHashHelper
    {
        public static string ComputeSha256(string value)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);

            var bytes = Encoding.UTF8.GetBytes(value);
            var hash = SHA256.HashData(bytes);
            return Convert.ToHexString(hash);
        }

        public static string GetPrefix(string token, int length = 8)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(token);

            return token.Length <= length
                ? token
                : token[..length];
        }

        public static string GenerateToken(int length = 48)
        {
            var token = $"fsc_{StrongTokenGenerator.GenerateUrlSafeStrong(length)}";
            return token;
        }
    }
}
