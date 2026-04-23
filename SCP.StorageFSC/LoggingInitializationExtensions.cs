using Serilog;

namespace SCP.StorageFSC;

public static class LoggingInitializationExtensions
{
    public static WebApplicationBuilder InitializeLogging(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var configuredLogPath = builder.Configuration["Paths:LogsPath"];

        var logFilePath = string.IsNullOrWhiteSpace(configuredLogPath)
            ? Path.Combine(Directory.GetCurrentDirectory(), "logs", "app-.log")
            : (Path.IsPathRooted(configuredLogPath)
                ? configuredLogPath
                : Path.Combine(Directory.GetCurrentDirectory(), configuredLogPath));

        var logDirectory = Path.GetDirectoryName(logFilePath);

        if (!string.IsNullOrWhiteSpace(logDirectory))
        {
            Directory.CreateDirectory(logDirectory);
        }

        builder.Services.AddSerilog((services, loggerConfiguration) => loggerConfiguration
            .ReadFrom.Configuration(builder.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithThreadId()
            .WriteTo.Console()
            .WriteTo.File(logFilePath, rollingInterval: RollingInterval.Day));

        builder.Host.UseSerilog();

        return builder;
    }
}
