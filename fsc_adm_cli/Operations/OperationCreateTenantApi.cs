using fsc_adm_cli.Infrastructure;
using fsc_adm_cli.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace fsc_adm_cli.Operations
{
    public sealed class OperationCreateTenantApi : IUnitOperation
    {
        public string Key => "create-tenant-api";

        public string Description => "Creates a new tenant and API token.";

        public string Usage =>
            """
            fsc_adm_cli create-tenant-api --tenant <name> [options]

            Required:
              --tenant <name>              Tenant name

            Optional:
              --token <name>               API token name (default: "<tenant> API")
              --read                       Include read scope
              --write                      Include write scope
              --delete                     Include delete scope
              --admin                      Create admin token
              --expires-utc <value>        Token expiration, for example 2026-12-31T23:59:59Z
              --service-url <url>          Service base URL
              --admin-conf <path>          Path to admin.conf

            If no permission flags are passed, defaults are: read + write + delete.
            """;

        public async Task<int> ExecuteAsync(IServiceProvider services, string[] args, CancellationToken cancellationToken = default)
        {
            var tenantName = CliArgReader.GetValue(args, "--tenant");
            if (string.IsNullOrWhiteSpace(tenantName))
            {
                Console.Error.WriteLine("Missing required option: --tenant");
                Console.WriteLine(Usage);
                return 2;
            }

            var tokenName = CliArgReader.GetValue(args, "--token") ?? $"{tenantName} API";
            var explicitRead = CliArgReader.HasFlag(args, "--read");
            var explicitWrite = CliArgReader.HasFlag(args, "--write");
            var explicitDelete = CliArgReader.HasFlag(args, "--delete");
            var explicitAdmin = CliArgReader.HasFlag(args, "--admin");
            var anyPermissionsSpecified = explicitRead || explicitWrite || explicitDelete || explicitAdmin;

            var request = new CreateApiTokenRequest
            {
                Name = tokenName,
                CanRead = anyPermissionsSpecified ? explicitRead : true,
                CanWrite = anyPermissionsSpecified ? explicitWrite : true,
                CanDelete = anyPermissionsSpecified ? explicitDelete : true,
                IsAdmin = explicitAdmin,
                ExpiresUtc = CliArgReader.GetDateTimeOffset(args, "--expires-utc")
            };

            var factory = services.GetRequiredService<AdminCliClientFactory>();
            await using var client = await factory.CreateAsync(args, cancellationToken).ConfigureAwait(false);

            Console.WriteLine($"Creating tenant '{tenantName}'...");
            var tenant = await client.CreateTenantAsync(tenantName, cancellationToken).ConfigureAwait(false);

            Console.WriteLine($"Creating API token '{request.Name}'...");
            request.TenantId = tenant.Id;
            var createdToken = await client.CreateApiTokenAsync(request, cancellationToken).ConfigureAwait(false);

            Console.WriteLine("Tenant created:");
            Console.WriteLine($"  Id:         {tenant.Id}");
            Console.WriteLine($"  TenantGuid: {tenant.TenantGuid}");
            Console.WriteLine($"  Name:       {tenant.Name}");
            Console.WriteLine();
            Console.WriteLine("API token created:");
            Console.WriteLine($"  TokenId:       {createdToken.Token.Id}");
            Console.WriteLine($"  TokenName:     {createdToken.Token.Name}");
            Console.WriteLine($"  TokenPrefix:   {createdToken.Token.TokenPrefix}");
            Console.WriteLine($"  PlainTextToken:{Environment.NewLine}{createdToken.PlainTextToken}");

            return 0;
        }

        public void ConfigureServices(HostApplicationBuilder builder)
        {
        }
    }
}
