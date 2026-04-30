using Microsoft.Extensions.Hosting;

namespace fsc_adm_cli.Operations
{
    public interface IUnitOperation
    {
        string Key { get; }
        string Description { get; }
        string Usage { get; }
        Task<int> ExecuteAsync(IServiceProvider services, string[] args, CancellationToken cancellationToken = default);
        void ConfigureServices(HostApplicationBuilder builder);
    }
}
