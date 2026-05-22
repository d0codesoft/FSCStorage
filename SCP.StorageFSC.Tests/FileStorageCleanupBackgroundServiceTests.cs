using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using scp.filestorage.Services;
using SCP.StorageFSC.Services;

namespace SCP.StorageFSC.Tests;

public sealed class FileStorageCleanupBackgroundServiceTests
{
    [Fact]
    public async Task StopAsync_WhenWaitingForInterval_CompletesWithoutCanceledTask()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var serviceProvider = new ServiceCollection().BuildServiceProvider();
        var runtimeOptions = new SystemSettingsRuntimeOptions();
        runtimeOptions.UpdateCleanup(new FileStorageCleanupOptions
        {
            Enabled = false,
            InitialDelay = TimeSpan.Zero,
            Interval = TimeSpan.FromDays(1)
        });

        using var service = new FileStorageCleanupBackgroundService(
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            runtimeOptions,
            NullLogger<FileStorageCleanupBackgroundService>.Instance);

        await service.StartAsync(cancellationToken);
        await Task.Delay(50, cancellationToken);
        await service.StopAsync(cancellationToken);

        Assert.False(service.ExecuteTask?.IsFaulted);
        Assert.False(service.ExecuteTask?.IsCanceled);
    }
}
