using fsc_adm_cli.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace fsc_adm_cli.Operations
{
    public static class OperationModuleExtensions
    {
        public static IServiceCollection AddUnitOperationsFromEntryAssembly(this IServiceCollection services)
        {
            services.AddSingleton<AdminCliClientFactory>();

            var assembly = Assembly.GetExecutingAssembly();
            var operationTypes = assembly
                .GetTypes()
                .Where(type => !type.IsAbstract && !type.IsInterface && typeof(IUnitOperation).IsAssignableFrom(type));

            foreach (var operationType in operationTypes)
            {
                services.AddSingleton(typeof(IUnitOperation), operationType);
            }

            return services;
        }

        public static ManagerOperation BuildManagerOperation(this IServiceProvider serviceProvider)
        {
            var manager = new ManagerOperation();
            var operations = serviceProvider.GetServices<IUnitOperation>();

            foreach (var operation in operations)
                manager.Register(operation);

            return manager;
        }
    }
}
