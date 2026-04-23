namespace scp.filestorage.Data.Dto
{
    public sealed class ApiTokenValidationResult
    {
        public bool Success { get; init; }
        public Guid TokenId { get; init; }
        public Guid? TenantId { get; init; }
        public bool IsAdmin { get; init; }
        public string Name { get; init; } = "";
        public IReadOnlyCollection<string> Roles { get; init; } = Array.Empty<string>();
        public IReadOnlyCollection<string> Scopes { get; init; } = Array.Empty<string>();
    }
}
