using System.Text.Json;

namespace scp_fs_cli.Infrastructure
{
    public static class ClientConfigReader
    {
        public static async Task<ClientConfig> ReadAsync(string path, CancellationToken cancellationToken = default)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException($"Config file not found: {path}");

            await using var stream = File.OpenRead(path);
            var config = await JsonSerializer.DeserializeAsync<ClientConfig>(
                stream,
                JsonOptions.Default,
                cancellationToken).ConfigureAwait(false);

            if (config is null)
                throw new InvalidOperationException("client.config is empty or invalid.");

            if (string.IsNullOrWhiteSpace(config.ServiceUrl))
                throw new InvalidOperationException("client.config: serviceUrl is required.");

            if (string.IsNullOrWhiteSpace(config.ApiToken))
                throw new InvalidOperationException("client.config: apiToken is required.");

            if (config.TenantId == Guid.Empty)
                throw new InvalidOperationException("client.config: tenantId is required.");

            return config;
        }
    }
}
