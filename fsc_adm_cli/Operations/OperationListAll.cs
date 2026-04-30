using fsc_adm_cli.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace fsc_adm_cli.Operations
{
    public sealed class OperationListAll : IUnitOperation
    {
        public string Key => "list-all";

        public string Description => "Lists all tenants and their API tokens.";

        public string Usage =>
            """
            fsc_adm_cli list-all [options]

            Optional:
              --json                       Print raw JSON-like output
              --service-url <url>          Service base URL
              --admin-conf <path>          Path to admin.conf
            """;

        public async Task<int> ExecuteAsync(IServiceProvider services, string[] args, CancellationToken cancellationToken = default)
        {
            var factory = services.GetRequiredService<AdminCliClientFactory>();
            await using var client = await factory.CreateAsync(args, cancellationToken).ConfigureAwait(false);

            var tenants = await client.GetTenantsAsync(cancellationToken).ConfigureAwait(false);
            if (tenants.Count == 0)
            {
                Console.WriteLine("No tenants found.");
                return 0;
            }

            var asJson = CliArgReader.HasFlag(args, "--json");
            if (asJson)
            {
                var payload = new List<object>();
                foreach (var tenant in tenants)
                {
                    var tokens = await client.GetTenantTokensAsync(tenant.Id, cancellationToken).ConfigureAwait(false);
                    payload.Add(new
                    {
                        tenant.Id,
                        tenant.TenantGuid,
                        tenant.Name,
                        tenant.IsActive,
                        Tokens = tokens
                    });
                }

                Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(payload, JsonOptions.Pretty));
                return 0;
            }

            Console.WriteLine($"Tenants: {tenants.Count}");
            foreach (var tenant in tenants.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
            {
                Console.WriteLine();
                Console.WriteLine($"Tenant: {tenant.Name}");
                Console.WriteLine($"  Id:         {tenant.Id}");
                Console.WriteLine($"  TenantGuid: {tenant.TenantGuid}");
                Console.WriteLine($"  Active:     {tenant.IsActive}");

                var tokens = await client.GetTenantTokensAsync(tenant.Id, cancellationToken).ConfigureAwait(false);
                if (tokens.Count == 0)
                {
                    Console.WriteLine("  Tokens:     <none>");
                    continue;
                }

                Console.WriteLine("  Tokens:");
                foreach (var token in tokens.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"    - {token.Name}");
                    Console.WriteLine($"      Id:         {token.Id}");
                    Console.WriteLine($"      Prefix:     {token.TokenPrefix}");
                    Console.WriteLine($"      Active:     {token.IsActive}");
                    Console.WriteLine($"      Read/Write/Delete/Admin: {token.CanRead}/{token.CanWrite}/{token.CanDelete}/{token.IsAdmin}");
                    Console.WriteLine($"      ExpiresUtc: {token.ExpiresUtc?.ToString("O") ?? "<none>"}");
                }
            }

            return 0;
        }

        public void ConfigureServices(HostApplicationBuilder builder)
        {
        }
    }
}
