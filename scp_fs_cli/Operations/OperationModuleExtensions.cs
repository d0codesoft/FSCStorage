using scp_fs_cli.Infrastructure;
using scp_fs_cli.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace scp_fs_cli.Operations
{
    public static class OperationModuleExtensions
    {
        public static IServiceCollection AddUnitOperationsFromEntryAssembly(this IServiceCollection services)
        {
            services.AddSingleton<ClientCliClientFactory>();
            services.AddSingleton<FileUploadService>();

            var assembly = Assembly.GetExecutingAssembly();
            var operationTypes = assembly
                .GetTypes()
                .Where(type => !type.IsAbstract && !type.IsInterface && typeof(IUnitOperation).IsAssignableFrom(type));

            foreach (var operationType in operationTypes)
                services.AddSingleton(typeof(IUnitOperation), operationType);

            return services;
        }

        public static ManagerOperation BuildManagerOperation(this IServiceProvider serviceProvider)
        {
            var manager = new ManagerOperation();

            foreach (var operation in serviceProvider.GetServices<IUnitOperation>())
                manager.Register(operation);

            return manager;
        }
    }
}
