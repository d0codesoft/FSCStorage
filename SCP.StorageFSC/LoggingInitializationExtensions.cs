using Serilog;

namespace SCP.StorageFSC;

public static class LoggingInitializationExtensions
{
    public static WebApplicationBuilder InitializeLogging(this WebApplicationBuilder builder, ApplicationPaths applicationPaths)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(applicationPaths);

        Serilog.Debugging.SelfLog.Enable(msg =>
        {
            File.AppendAllText(
                Path.Combine(AppContext.BaseDirectory, "serilog-selflog.txt"),
                msg);
        });

        builder.Host.UseSerilog((context, services, loggerConfiguration) =>
        {
            Directory.CreateDirectory(applicationPaths.LogsPath);

            var logFilePath = Path.Combine(applicationPaths.LogsPath, "app-.log");

            loggerConfiguration
                .ReadFrom.Configuration(context.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .Enrich.WithMachineName()
                .Enrich.WithThreadId()
                .WriteTo.Console()
                .WriteTo.File(
                    logFilePath,
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 30);
        });

        return builder;
    }

    public static WebApplicationBuilder InitializeDataFolder(this WebApplicationBuilder builder, ApplicationPaths applicationPaths)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(applicationPaths);

        using var startupLogger = CreateStartupLogger(applicationPaths);

        try
        {
            Directory.CreateDirectory(applicationPaths.BasePath);
            Directory.CreateDirectory(applicationPaths.LogsPath);
            Directory.CreateDirectory(applicationPaths.DataPath);
        }
        catch (Exception ex)
        {
            startupLogger.Error(ex, "Failed to create application directories");
            throw;
        }

        return builder;
    }

    private static Serilog.Core.Logger CreateStartupLogger(ApplicationPaths applicationPaths)
    {
        var loggerConfiguration = new LoggerConfiguration()
            .WriteTo.Console();

        try
        {
            var logFilePath = Path.Combine(applicationPaths.LogsPath, "startup-.log");

            loggerConfiguration.WriteTo.File(
                logFilePath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7);
        }
        catch
        {
            // Keep console logging available even if the configured log path itself is invalid.
        }

        return loggerConfiguration.CreateLogger();
    }
}
