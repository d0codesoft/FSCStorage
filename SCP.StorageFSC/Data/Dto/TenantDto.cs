namespace SCP.StorageFSC.Data.Dto
{
    public sealed class CreateTenantRequest
    {
        public Guid UserId { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public sealed class UpdateTenantRequest
    {
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
    }

    public sealed class TenantDto
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public Guid TenantGuid { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime CreatedUtc { get; set; }
        public DateTime? UpdatedUtc { get; set; }
    }

    public sealed class CreateApiTokenRequest
    {
        public Guid UserId { get; set; }
        public Guid TenantId { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool CanRead { get; set; } = true;
        public bool CanWrite { get; set; }
        public bool CanDelete { get; set; }
        public bool IsAdmin { get; set; }
        public DateTime? ExpiresUtc { get; set; }
    }

    public sealed class UpdateApiTokenRequest
    {
        public string Name { get; set; } = string.Empty;
        public bool CanRead { get; set; } = true;
        public bool CanWrite { get; set; }
        public bool CanDelete { get; set; }
        public bool IsAdmin { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime? ExpiresUtc { get; set; }
    }

    public sealed class ApiTokenDto
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public Guid TenantId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string TokenPrefix { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public bool CanRead { get; set; }
        public bool CanWrite { get; set; }
        public bool CanDelete { get; set; }
        public bool IsAdmin { get; set; }
        public DateTime CreatedUtc { get; set; }
        public DateTime? LastUsedUtc { get; set; }
        public DateTime? ExpiresUtc { get; set; }
        public DateTime? RevokedUtc { get; set; }
    }

    public sealed class UserTenantsDto
    {
        public Guid UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string? Email { get; set; }
        public bool IsActive { get; set; }
        public IReadOnlyList<TenantDto> Tenants { get; set; } = [];
    }

    public sealed class CreateUserRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string Password { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public bool IsAdmin { get; set; }
    }

    public sealed class UpdateUserRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? Password { get; set; }
        public bool IsActive { get; set; } = true;
        public bool IsAdmin { get; set; }
    }

    public sealed class UserApiTokenDto
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public Guid TenantId { get; set; }
        public string TenantName { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string TokenPrefix { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public bool CanRead { get; set; }
        public bool CanWrite { get; set; }
        public bool CanDelete { get; set; }
        public bool IsAdmin { get; set; }
        public DateTime CreatedUtc { get; set; }
        public DateTime? LastUsedUtc { get; set; }
        public DateTime? ExpiresUtc { get; set; }
        public DateTime? RevokedUtc { get; set; }
    }

    public sealed class UserManagementDto
    {
        public Guid UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string? Email { get; set; }
        public bool IsActive { get; set; }
        public bool IsLocked { get; set; }
        public DateTime? LockedUntilUtc { get; set; }
        public bool IsAdmin { get; set; }
        public DateTime CreatedUtc { get; set; }
        public DateTime? UpdatedUtc { get; set; }
        public IReadOnlyList<TenantDto> Tenants { get; set; } = [];
        public IReadOnlyList<UserApiTokenDto> ApiTokens { get; set; } = [];
    }

    public sealed class CreatedApiTokenResult
    {
        public ApiTokenDto Token { get; set; } = new();
        public string PlainTextToken { get; set; } = string.Empty;
    }

    public sealed class CreatedApiTokenResponse
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string TokenPrefix { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
        public string[] Scopes { get; set; } = [];
        public bool IsAdmin { get; set; }
        public DateTime? ExpiresUtc { get; set; }

        public static CreatedApiTokenResponse FromResult(CreatedApiTokenResult result)
        {
            return new CreatedApiTokenResponse
            {
                Id = result.Token.Id,
                TenantId = result.Token.TenantId,
                Name = result.Token.Name,
                TokenPrefix = result.Token.TokenPrefix,
                Token = result.PlainTextToken,
                Scopes = ApiTokenScopes.FromPermissions(
                    result.Token.CanRead,
                    result.Token.CanWrite,
                    result.Token.CanDelete),
                IsAdmin = result.Token.IsAdmin,
                ExpiresUtc = result.Token.ExpiresUtc
            };
        }
    }

    public static class ApiTokenScopes
    {
        public static string[] FromPermissions(bool canRead, bool canWrite, bool canDelete)
        {
            var scopes = new List<string>(3);

            if (canRead)
                scopes.Add("files.read");

            if (canWrite)
                scopes.Add("files.write");

            if (canDelete)
                scopes.Add("files.delete");

            return scopes.ToArray();
        }
    }
}
