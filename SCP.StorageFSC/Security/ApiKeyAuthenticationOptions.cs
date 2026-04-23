using Microsoft.AspNetCore.Authentication;

namespace scp.filestorage.Security
{
    public sealed class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
    {
        public string HeaderName { get; set; } = "X-Api-Key";
        public string SchemeName { get; set; } = "ApiKey";
    }
}
