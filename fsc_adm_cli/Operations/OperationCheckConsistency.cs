using fsc_adm_cli.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace fsc_adm_cli.Operations
{
    public sealed class OperationCheckConsistency : IUnitOperation
    {
        public string Key => "check-consistency";

        public string Description => "Queues file storage database consistency check.";

        public string Usage =>
            """
            fsc_adm_cli check-consistency [options]

            Optional:
              --service-url <url>         Service base URL
              --admin-conf <path>         Path to admin.conf
            """;

        public void ConfigureServices(HostApplicationBuilder builder)
        {
        }

        public async Task<int> ExecuteAsync(
            IServiceProvider services,
            string[] args,
            CancellationToken cancellationToken = default)
        {
            var factory = services.GetRequiredService<AdminCliClientFactory>();
            await using var client = await factory.CreateAsync(args, cancellationToken).ConfigureAwait(false);

            var task = await client.QueueFileStorageConsistencyCheckAsync(cancellationToken).ConfigureAwait(false);

            Console.WriteLine("File storage consistency check queued.");
            Console.WriteLine($"TaskId: {task.TaskId}");
            Console.WriteLine($"Type: {task.Type}");
            Console.WriteLine($"CreatedAtUtc: {task.CreatedAtUtc:O}");

            return 0;
        }
    }
}
