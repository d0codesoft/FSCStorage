using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using scp.filestorage.Services;

namespace SCP.StorageFSC.Tests;

public sealed class FileStorageCleanupBackgroundServiceTests
{
    [Fact]
    public async Task StopAsync_WhenWaitingForInterval_CompletesWithoutCanceledTask()
    {
        await using var serviceProvider = new ServiceCollection().BuildServiceProvider();
        var options = new TestOptionsMonitor(new FileStorageCleanupOptions
        {
            Enabled = false,
            InitialDelay = TimeSpan.Zero,
            Interval = TimeSpan.FromDays(1)
        });

        using var service = new FileStorageCleanupBackgroundService(
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            options,
            NullLogger<FileStorageCleanupBackgroundService>.Instance);

        await service.StartAsync(CancellationToken.None);
        await Task.Delay(50);
        await service.StopAsync(CancellationToken.None);

        Assert.False(service.ExecuteTask?.IsFaulted);
        Assert.False(service.ExecuteTask?.IsCanceled);
    }

    private sealed class TestOptionsMonitor : IOptionsMonitor<FileStorageCleanupOptions>
    {
        public TestOptionsMonitor(FileStorageCleanupOptions currentValue)
        {
            CurrentValue = currentValue;
        }

        public FileStorageCleanupOptions CurrentValue { get; }

        public FileStorageCleanupOptions Get(string? name)
        {
            return CurrentValue;
        }

        public IDisposable? OnChange(Action<FileStorageCleanupOptions, string?> listener)
        {
            return null;
        }
    }
}
