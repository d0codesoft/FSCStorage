using scp_fs_cli.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using scp_fs_cli.Services;

namespace scp_fs_cli.Operations
{
    public sealed class OperationStatus : IUnitOperation
    {
        public string Key => "status";
        public string UsageSignature => "status <uploadId>";
        public string Description => "Get multipart upload status by uploadId.";

        public void ConfigureServices(HostApplicationBuilder builder)
        {
        }

        public async Task<int> ExecuteAsync(IServiceProvider services, string[] args, CancellationToken cancellationToken = default)
        {
            try
            {
                var reader = new CliArgReader(args);
                var configPath = reader.ReadOptionalValue("--config");
                var uploadIdRaw = reader.ReadRequiredValue("uploadId");

                if (reader.HasUnknownOptions("--config"))
                    throw new ArgumentException($"Unknown option: {reader.FirstUnknownOption}");

                if (!Guid.TryParse(uploadIdRaw, out var uploadId))
                    throw new ArgumentException("uploadId must be a valid GUID.");

                var factory = services.GetRequiredService<ClientCliClientFactory>();
                using var client = await factory.CreateAsync(configPath, cancellationToken).ConfigureAwait(false);

                Console.WriteLine($"Service: {client.ServiceUrl}");
                Console.WriteLine($"UploadId: {uploadId}");
                var status = await client.GetMultipartStatusAsync(uploadId, cancellationToken).ConfigureAwait(false);
                Console.WriteLine(status.ToDisplayJson());

                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 1;
            }
        }
    }
}
