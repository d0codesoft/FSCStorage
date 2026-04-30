using scp_fs_cli;
using scp_fs_cli.Operations;
using Microsoft.Extensions.Hosting;
using System.Reflection;

const string AppName = "scp_fs_cli";
const string AppDescription = "scp_fs_cli client for scp.filestorage.";

var version = Assembly.GetExecutingAssembly()
    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
    ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString()
    ?? "unknown";

var bootstrapBuilder = Host.CreateApplicationBuilder(args);
bootstrapBuilder.Services.AddUnitOperationsFromEntryAssembly();

using var bootstrapHost = bootstrapBuilder.Build();
var managerOperation = bootstrapHost.Services.BuildManagerOperation();

if (args.Length == 0 || args.Contains("-?") || args.Contains("-h") || args.Contains("--help"))
{
    PrintHelp.Print(AppName, AppDescription, managerOperation);
    return;
}

if (args.Contains("--version"))
{
    Console.WriteLine(version);
    return;
}

var operationKey = args[0];
if (!managerOperation.TryGet(operationKey, out var operation) || operation is null)
{
    PrintHelp.Print(AppName, AppDescription, managerOperation);
    Console.Error.WriteLine($"Unknown command: '{operationKey}'");
    Environment.ExitCode = 2;
    return;
}

var finalBuilder = Host.CreateApplicationBuilder(args);
finalBuilder.Services.AddUnitOperationsFromEntryAssembly();
operation.ConfigureServices(finalBuilder);

using var host = finalBuilder.Build();
Environment.ExitCode = await operation.ExecuteAsync(host.Services, args[1..]).ConfigureAwait(false);
