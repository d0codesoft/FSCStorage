using fsc_adm_cli;
using fsc_adm_cli.Infrastructure;
using fsc_adm_cli.Operations;
using Microsoft.Extensions.Hosting;
using System.Reflection;

const string AppName = "fsc_adm_cli";
const string AppDescription = "Administrative console utility for scp.filestorage.";

var version = Assembly.GetExecutingAssembly()
    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
    ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString()
    ?? "unknown";

var bootstrapBuilder = Host.CreateApplicationBuilder(args);
bootstrapBuilder.Services.AddUnitOperationsFromEntryAssembly();

using var bootstrapHost = bootstrapBuilder.Build();
var managerOperation = bootstrapHost.Services.BuildManagerOperation();

if (args.Length == 0 || args.Contains("-h") || args.Contains("--help") || args.Contains("/?"))
{
    PrintHelp.Print(AppName, version, AppDescription, managerOperation);
    return;
}

var operationKey = args[0];
if (!managerOperation.TryGet(operationKey, out var operation) || operation is null)
{
    PrintHelp.Print(AppName, version, AppDescription, managerOperation);
    Console.Error.WriteLine($"Unknown operation: '{operationKey}'");
    Environment.ExitCode = 2;
    return;
}

var finalBuilder = Host.CreateApplicationBuilder(args);
finalBuilder.Services.AddUnitOperationsFromEntryAssembly();
operation.ConfigureServices(finalBuilder);

using var host = finalBuilder.Build();
try
{
    var exitCode = await operation.ExecuteAsync(host.Services, args[1..]).ConfigureAwait(false);
    Environment.ExitCode = exitCode;
}
catch (AdminConfException ex)
{
    Console.Error.WriteLine($"Failed to read admin configuration: {ex.Message}");
    Environment.ExitCode = 2;
}
catch (HttpRequestException ex)
{
    Console.Error.WriteLine($"Failed to call FSC service: {ex.Message}");
    Environment.ExitCode = 1;
}
