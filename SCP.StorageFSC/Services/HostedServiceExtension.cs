namespace scp.filestorage.Services
{
    public static class HostedServiceExtension
    {
        public static void StopHostedService<THostedService>(IServiceProvider services, ILogger logger)
                where THostedService : class, IHostedService
        {
            var hostedService = services
                .GetServices<IHostedService>()
                .OfType<THostedService>()
                .FirstOrDefault();

            if (hostedService is null)
            {
                logger.LogWarning("Hosted service {HostedServiceType} is not registered.", typeof(THostedService).Name);
                return;
            }

            hostedService.StopAsync(CancellationToken.None).GetAwaiter().GetResult();
        }
    }
}
