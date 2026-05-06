using Microsoft.AspNetCore.Identity;
using SCP.StorageFSC.Data.Models;

namespace scp.filestorage.Services.Auth
{
    public sealed class PasswordHashService : IPasswordHashService
    {
        private readonly PasswordHasher<User> _passwordHasher = new();

        public string HashPassword(User user, string password)
        {
            ArgumentNullException.ThrowIfNull(user);

            if (string.IsNullOrWhiteSpace(password))
                throw new ArgumentException("Password cannot be empty.", nameof(password));

            return _passwordHasher.HashPassword(user, password);
        }

        public bool VerifyPassword(User user, string password)
        {
            ArgumentNullException.ThrowIfNull(user);

            if (string.IsNullOrEmpty(user.PasswordHash))
                return false;

            if (string.IsNullOrEmpty(password))
                return false;

            var result = _passwordHasher.VerifyHashedPassword(
                user,
                user.PasswordHash,
                password);

            return result is PasswordVerificationResult.Success
                or PasswordVerificationResult.SuccessRehashNeeded;
        }
    }
}
