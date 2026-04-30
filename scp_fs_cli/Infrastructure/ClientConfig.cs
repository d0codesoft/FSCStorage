using System.Text.Json.Serialization;

namespace scp_fs_cli.Infrastructure
{
    public sealed record ClientConfig(
        [property: JsonPropertyName("serviceUrl")] string ServiceUrl,
        [property: JsonPropertyName("apiToken")] string ApiToken,
        [property: JsonPropertyName("tenantId")] Guid TenantId);
}
