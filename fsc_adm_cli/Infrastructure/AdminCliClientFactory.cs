using fsc_adm_cli.Services;

namespace fsc_adm_cli.Infrastructure
{
    public sealed class AdminCliClientFactory
    {
        public async Task<FscAdminApiClient> CreateAsync(string[] args, CancellationToken cancellationToken = default)
        {
            var serviceUrl = CliArgReader.GetValue(args, "--service-url")
                ?? Environment.GetEnvironmentVariable("FSC_SERVICE_URL")
                ?? "https://localhost:5770";

            var adminConfPath = ResolveAdminConfPath(CliArgReader.GetValue(args, "--admin-conf"));
            var adminConf = await AdminConfReader.ReadAsync(adminConfPath, cancellationToken).ConfigureAwait(false);

            return new FscAdminApiClient(serviceUrl, adminConf.Key);
        }

        private static string ResolveAdminConfPath(string? explicitPath)
        {
            if (!string.IsNullOrWhiteSpace(explicitPath))
                return Path.GetFullPath(explicitPath);

            var envPath = Environment.GetEnvironmentVariable("FSC_ADMIN_CONF");
            if (!string.IsNullOrWhiteSpace(envPath))
                return Path.GetFullPath(envPath);

            var candidates = new[]
            {
                Path.Combine(Environment.CurrentDirectory, "admin.conf"),
                Path.Combine(AppContext.BaseDirectory, "admin.conf"),
                Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "bin", "Debug", "net10.0", "admin.conf"))
            };

            return candidates.FirstOrDefault(File.Exists) ?? candidates[0];
        }
    }
}
