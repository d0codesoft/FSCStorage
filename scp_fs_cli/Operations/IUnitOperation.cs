using Microsoft.Extensions.Hosting;

namespace scp_fs_cli.Operations
{
    public interface IUnitOperation
    {
        string Key { get; }
        string UsageSignature { get; }
        string Description { get; }
        Task<int> ExecuteAsync(IServiceProvider services, string[] args, CancellationToken cancellationToken = default);
        void ConfigureServices(HostApplicationBuilder builder);
    }
}
