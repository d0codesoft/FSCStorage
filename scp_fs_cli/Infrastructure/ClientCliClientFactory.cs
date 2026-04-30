using scp_fs_cli.Services;

namespace scp_fs_cli.Infrastructure
{
    public sealed class ClientCliClientFactory
    {
        public async Task<FscFileStorageClient> CreateAsync(string? configPath, CancellationToken cancellationToken = default)
        {
            var resolvedConfigPath = ResolveConfigPath(configPath);
            var config = await ClientConfigReader.ReadAsync(resolvedConfigPath, cancellationToken).ConfigureAwait(false);
            return new FscFileStorageClient(config);
        }

        private static string ResolveConfigPath(string? explicitPath)
        {
            if (!string.IsNullOrWhiteSpace(explicitPath))
                return Path.GetFullPath(explicitPath);

            return Path.Combine(AppContext.BaseDirectory, "client.config");
        }
    }
}
