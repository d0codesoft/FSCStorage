using scp.filestorage.Data.Handlers;
using SCP.StorageFSC.InterfacesService;

namespace SCP.StorageFSC.Data
{
    public static class ApplicationInitializationExtensions
    {
        public static IServiceCollection RegisterDatabase(this IServiceCollection services)
        {
            DapperTypeHandlers.Register();
            return services;
        }

        /// <summary>
        /// Initializes the database structure at application startup.
        /// </summary>
        public static async Task<WebApplication> InitializeDatabaseAsync(
            this WebApplication app,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(app);

            using var scope = app.Services.CreateScope();

            var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
                .CreateLogger("DatabaseInitialization");

            try
            {
                logger.LogInformation("Starting database initialization.");

                var dbInitializer = scope.ServiceProvider.GetRequiredService<IDbInitializer>();
                await dbInitializer.InitializeAsync(cancellationToken);
                await dbInitializer.InitializeDefaultValuesAsync(cancellationToken);

                logger.LogInformation("Database initialization completed successfully.");
            }
            catch (Exception ex)
            {
                logger.LogCritical(ex, "Database initialization failed.");
                throw;
            }

            return app;
        }

        public static async Task<WebApplication> LoadSystemSettingsAsync(
            this WebApplication app,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(app);

            using var scope = app.Services.CreateScope();
            var settingsService = scope.ServiceProvider.GetRequiredService<ISystemSettingsService>();
            await settingsService.LoadFileTransferLimiterSettingsAsync(cancellationToken);

            return app;
        }
    }
}
