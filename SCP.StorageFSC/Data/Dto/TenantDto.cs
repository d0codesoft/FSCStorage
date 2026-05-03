namespace SCP.StorageFSC.Data.Dto
{
    public sealed class CreateTenantRequest
    {
        public string Name { get; set; } = string.Empty;
    }

    public sealed class TenantDto
    {
        public Guid Id { get; set; }
        public Guid TenantGuid { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime CreatedUtc { get; set; }
        public DateTime? UpdatedUtc { get; set; }
    }

    public sealed class CreateApiTokenRequest
    {
        public Guid TenantId { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool CanRead { get; set; } = true;
        public bool CanWrite { get; set; }
        public bool CanDelete { get; set; }
        public bool IsAdmin { get; set; }
        public DateTime? ExpiresUtc { get; set; }
    }

    public sealed class ApiTokenDto
    {
        public Guid Id { get; set; }
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
