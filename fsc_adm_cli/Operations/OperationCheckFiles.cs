using fsc_adm_cli.Infrastructure;
using fsc_adm_cli.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Security.Cryptography;
using System.Text;

namespace fsc_adm_cli.Operations
{
    public sealed class OperationCheckFiles : IUnitOperation
    {
        public string Key => "check-files";

        public string Description => "Verifies file save/list/info/download/delete using a temporary tenant API token.";

        public string Usage =>
            """
            fsc_adm_cli check-files [options]

            Optional:
              --tenant-id <guid>          Target tenant id. If omitted, current admin tenant is used.
              --category <value>          Category for the test file
              --external-key <value>      External key for the test file
              --keep-token                Do not revoke the temporary token after the check
              --service-url <url>         Service base URL
              --admin-conf <path>         Path to admin.conf
            """;

        public async Task<int> ExecuteAsync(IServiceProvider services, string[] args, CancellationToken cancellationToken = default)
        {
            var factory = services.GetRequiredService<AdminCliClientFactory>();
            await using var client = await factory.CreateAsync(args, cancellationToken).ConfigureAwait(false);

            var targetTenantId = CliArgReader.GetGuid(args, "--tenant-id");
            TenantDto targetTenant;

            if (targetTenantId.HasValue)
            {
                targetTenant = await client.GetTenantByIdAsync(targetTenantId.Value, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                targetTenant = await client.GetMyTenantAsync(cancellationToken).ConfigureAwait(false)
                    ?? throw new InvalidOperationException("Current tenant cannot be resolved from admin token.");
            }

            Console.WriteLine($"Target tenant: {targetTenant.Name} ({targetTenant.Id})");

            var temporaryToken = await client.CreateApiTokenAsync(
                new CreateApiTokenRequest
                {
                    TenantId = targetTenant.Id,
                    Name = $"fsc_adm_cli_check_{DateTime.UtcNow:yyyyMMddHHmmss}",
                    CanRead = true,
                    CanWrite = true,
                    CanDelete = true,
                    IsAdmin = false,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(30)
                },
                cancellationToken).ConfigureAwait(false);

            var keepToken = CliArgReader.HasFlag(args, "--keep-token");
            var category = CliArgReader.GetValue(args, "--category");
            var externalKey = CliArgReader.GetValue(args, "--external-key");

            Guid? uploadedFileGuid = null;
            var fileName = $"fsc_check_{DateTime.UtcNow:yyyyMMddHHmmss}.txt";
            var fileBytes = Encoding.UTF8.GetBytes($"fsc_adm_cli check file {DateTime.UtcNow:O}");
            var expectedSha256 = Convert.ToHexString(SHA256.HashData(fileBytes));

            try
            {
                Console.WriteLine("Step 1/5: upload test file...");
                var saveResult = await client.UploadBytesAsync(
                    temporaryToken.PlainTextToken,
                    fileName,
                    fileBytes,
                    category,
                    externalKey,
                    cancellationToken).ConfigureAwait(false);

                if (!saveResult.Success || saveResult.File is null)
                    throw new InvalidOperationException("Upload returned unsuccessful response.");

                uploadedFileGuid = saveResult.File.FileGuid;
                Console.WriteLine($"  Uploaded FileGuid: {uploadedFileGuid}");

                Console.WriteLine("Step 2/5: verify file is visible in list...");
                var files = await client.GetFilesAsync(temporaryToken.PlainTextToken, cancellationToken).ConfigureAwait(false);
                if (!files.Any(x => x.FileGuid == uploadedFileGuid.Value))
                    throw new InvalidOperationException("Uploaded file is not present in tenant file list.");

                Console.WriteLine("Step 3/5: verify file metadata...");
                var fileInfo = await client.GetFileInfoAsync(temporaryToken.PlainTextToken, uploadedFileGuid.Value, cancellationToken).ConfigureAwait(false)
                    ?? throw new InvalidOperationException("Uploaded file metadata cannot be read.");

                if (!string.Equals(fileInfo.FileName, fileName, StringComparison.Ordinal))
                    throw new InvalidOperationException($"Unexpected file name. Expected '{fileName}', actual '{fileInfo.FileName}'.");

                if (fileInfo.FileSize != fileBytes.Length)
                    throw new InvalidOperationException($"Unexpected file size. Expected {fileBytes.Length}, actual {fileInfo.FileSize}.");

                Console.WriteLine("Step 4/5: verify file download...");
                var downloadedBytes = await client.DownloadFileAsync(temporaryToken.PlainTextToken, uploadedFileGuid.Value, cancellationToken).ConfigureAwait(false);
                var actualSha256 = Convert.ToHexString(SHA256.HashData(downloadedBytes));
                if (!string.Equals(expectedSha256, actualSha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Downloaded file hash does not match uploaded content.");

                Console.WriteLine("Step 5/5: delete file and verify removal...");
                await client.DeleteFileAsync(temporaryToken.PlainTextToken, uploadedFileGuid.Value, cancellationToken).ConfigureAwait(false);

                var deletedInfo = await client.TryGetFileInfoAsync(temporaryToken.PlainTextToken, uploadedFileGuid.Value, cancellationToken).ConfigureAwait(false);
                if (deletedInfo is not null)
                    throw new InvalidOperationException("File still exists after delete.");

                Console.WriteLine("File check completed successfully.");
                return 0;
            }
            finally
            {
                if (!keepToken)
                {
                    try
                    {
                        await client.RevokeApiTokenAsync(temporaryToken.Token.Id, cancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"Warning: failed to revoke temporary token: {ex.Message}");
                    }
                }
                else
                {
                    Console.WriteLine($"Temporary token kept: {temporaryToken.Token.Id}");
                    Console.WriteLine($"PlainTextToken:{Environment.NewLine}{temporaryToken.PlainTextToken}");
                }
            }
        }

        public void ConfigureServices(HostApplicationBuilder builder)
        {
        }
    }
}
