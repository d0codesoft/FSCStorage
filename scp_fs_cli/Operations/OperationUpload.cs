using scp_fs_cli.Infrastructure;
using scp_fs_cli.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace scp_fs_cli.Operations
{
    public sealed class OperationUpload : IUnitOperation
    {
        public string Key => "upload";
        public string UsageSignature => "upload <file>";
        public string Description => "Upload a file to scp.filestorage.";

        public void ConfigureServices(HostApplicationBuilder builder)
        {
        }

        public async Task<int> ExecuteAsync(IServiceProvider services, string[] args, CancellationToken cancellationToken = default)
        {
            try
            {
                var reader = new CliArgReader(args);
                var threads = reader.ReadInt("--threads", 2);
                var retries = reader.ReadInt("--retries", 3);
                var configPath = reader.ReadOptionalValue("--config");
                var category = reader.ReadOptionalValue("--category");
                var externalKey = reader.ReadOptionalValue("--external-key");
                var filePath = reader.ReadRequiredValue("file");

                if (reader.HasUnknownOptions("--threads", "--retries", "--config", "--category", "--external-key"))
                    throw new ArgumentException($"Unknown option: {reader.FirstUnknownOption}");

                var uploadService = services.GetRequiredService<FileUploadService>();
                await uploadService.UploadAsync(
                    new FileInfo(filePath),
                    threads,
                    retries,
                    configPath,
                    category,
                    externalKey,
                    cancellationToken).ConfigureAwait(false);

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
