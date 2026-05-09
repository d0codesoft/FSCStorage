using System.ComponentModel.DataAnnotations;

namespace scp.filestorage.webui.Models
{
    public sealed class TenantViewModel
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public Guid TenantGuid { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime CreatedUtc { get; set; }
        public DateTime? UpdatedUtc { get; set; }
    }

    public sealed class TenantEditorModel
    {
        [Required(ErrorMessage = "Owner user is required.")]
        public Guid UserId { get; set; }

        [Required(ErrorMessage = "Tenant name is required.")]
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
    }

    public sealed class TenantUpsertRequest
    {
        public Guid UserId { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
    }

    public sealed class ApiTokenViewModel
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

        public string ScopesLabel =>
            string.Join(", ", GetScopes().DefaultIfEmpty("none"));

        private IEnumerable<string> GetScopes()
        {
            if (CanRead)
                yield return "read";

            if (CanWrite)
                yield return "write";

            if (CanDelete)
                yield return "delete";

            if (IsAdmin)
                yield return "admin";
        }
    }

    public sealed class ApiTokenEditorModel
    {
        [Required(ErrorMessage = "Token name is required.")]
        public string Name { get; set; } = string.Empty;
        public bool CanRead { get; set; } = true;
        public bool CanWrite { get; set; }
        public bool CanDelete { get; set; }
        public bool IsAdmin { get; set; }
        public bool IsActive { get; set; } = true;
        public string? ExpiresUtcText { get; set; }
    }

    public sealed class CreateApiTokenRequestModel
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

    public sealed class UpdateApiTokenRequestModel
    {
        public string Name { get; set; } = string.Empty;
        public bool CanRead { get; set; } = true;
        public bool CanWrite { get; set; }
        public bool CanDelete { get; set; }
        public bool IsAdmin { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime? ExpiresUtc { get; set; }
    }

    public sealed class CreatedApiTokenViewModel
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string TokenPrefix { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
        public string[] Scopes { get; set; } = [];
        public bool IsAdmin { get; set; }
        public DateTime? ExpiresUtc { get; set; }
    }

    public sealed class UserTenantsViewModel
    {
        public Guid UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string? Email { get; set; }
        public bool IsActive { get; set; }
        public IReadOnlyList<TenantViewModel> Tenants { get; set; } = [];
    }

    public sealed class UserApiTokenViewModel
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

        public string ScopesLabel =>
            string.Join(", ", GetScopes().DefaultIfEmpty("none"));

        private IEnumerable<string> GetScopes()
        {
            if (CanRead)
                yield return "read";

            if (CanWrite)
                yield return "write";

            if (CanDelete)
                yield return "delete";

            if (IsAdmin)
                yield return "admin";
        }
    }

    public sealed class UserManagementViewModel
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
        public IReadOnlyList<TenantViewModel> Tenants { get; set; } = [];
        public IReadOnlyList<UserApiTokenViewModel> ApiTokens { get; set; } = [];
    }

    public sealed class UserEditorModel
    {
        [Required(ErrorMessage = "User name is required.")]
        public string Name { get; set; } = string.Empty;

        [EmailAddress(ErrorMessage = "Enter a valid email address.")]
        public string? Email { get; set; }

        public string? Password { get; set; }
        public bool IsActive { get; set; } = true;
        public bool IsAdmin { get; set; }
    }

    public sealed class CreateUserRequestModel
    {
        public string Name { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string Password { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public bool IsAdmin { get; set; }
    }

    public sealed class UpdateUserRequestModel
    {
        public string Name { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? Password { get; set; }
        public bool IsActive { get; set; } = true;
        public bool IsAdmin { get; set; }
    }
}
