using SCP.StorageFSC.Data.Dto;
using SCP.StorageFSC.Data.Models;
using SCP.StorageFSC.Data.Repositories;
using SCP.StorageFSC.Security;
using SCP.StorageFSC.Services;
using Serilog.Core;
using Serilog.Events;

namespace SCP.StorageFSC.Tests;

public sealed class SystemSettingsServiceTests
{
    [Fact]
    public async Task UpdateAsync_WhenKnownFileTransferSetting_StoresValueAndAppliesLimiterSettings()
    {
        await using var limiter = new FileTransferLimiter(
            maxConcurrentUploads: 4,
            maxConcurrentDownloads: 20,
            uploadBytesPerSecond: 50L * 1024 * 1024,
            downloadBytesPerSecond: 100L * 1024 * 1024);
        var repository = new InMemorySystemSettingRepository();
        await SeedDefaultsAsync(repository, TestContext.Current.CancellationToken);
        var sut = CreateService(repository, limiter);

        var setting = await sut.UpdateAsync(
            SystemSettingsService.MaxConcurrentUploadsName,
            new UpdateSystemSettingRequest { Value = "8" },
            TestContext.Current.CancellationToken);

        Assert.Equal("8", setting.Value);
        Assert.Equal(8, limiter.CurrentSettings.MaxConcurrentUploads);
        Assert.Equal(20, limiter.CurrentSettings.MaxConcurrentDownloads);
    }

    [Fact]
    public async Task UpdateAsync_WhenValueIsInvalid_ThrowsArgumentException()
    {
        await using var limiter = new FileTransferLimiter(
            maxConcurrentUploads: 4,
            maxConcurrentDownloads: 20,
            uploadBytesPerSecond: 50L * 1024 * 1024,
            downloadBytesPerSecond: 100L * 1024 * 1024);
        var sut = new SystemSettingsService(
            new InMemorySystemSettingRepository(),
            new AdminCurrentTenantAccessor(),
            limiter,
            new SystemSettingsRuntimeOptions(),
            new LoggingLevelSwitch(LogEventLevel.Debug));

        await Assert.ThrowsAsync<ArgumentException>(() => sut.UpdateAsync(
            SystemSettingsService.UploadBytesPerSecondName,
            new UpdateSystemSettingRequest { Value = "0" },
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task LoadFileTransferLimiterSettingsAsync_AppliesPersistedSettings()
    {
        await using var limiter = new FileTransferLimiter(
            maxConcurrentUploads: 4,
            maxConcurrentDownloads: 20,
            uploadBytesPerSecond: 50L * 1024 * 1024,
            downloadBytesPerSecond: 100L * 1024 * 1024);
        var repository = new InMemorySystemSettingRepository();
        await SeedDefaultsAsync(repository, TestContext.Current.CancellationToken);
        await repository.UpsertAsync(new SystemSetting
        {
            Name = SystemSettingsService.MaxConcurrentDownloadsName,
            Value = "30",
            Description = "Maximum number of concurrent downloads."
        }, TestContext.Current.CancellationToken);
        var runtimeOptions = new SystemSettingsRuntimeOptions();
        var levelSwitch = new LoggingLevelSwitch(LogEventLevel.Debug);
        var sut = new SystemSettingsService(
            repository,
            new AdminCurrentTenantAccessor(),
            limiter,
            runtimeOptions,
            levelSwitch);

        await sut.LoadFileTransferLimiterSettingsAsync(TestContext.Current.CancellationToken);

        Assert.Equal(30, limiter.CurrentSettings.MaxConcurrentDownloads);
        Assert.Equal(5L * 1024 * 1024, runtimeOptions.Multipart.MinPartSizeBytes);
        Assert.True(runtimeOptions.Cleanup.Enabled);
        Assert.Equal(LogEventLevel.Debug, levelSwitch.MinimumLevel);
    }

    private static SystemSettingsService CreateService(
        ISystemSettingRepository repository,
        FileTransferLimiter limiter)
    {
        return new SystemSettingsService(
            repository,
            new AdminCurrentTenantAccessor(),
            limiter,
            new SystemSettingsRuntimeOptions(),
            new LoggingLevelSwitch(LogEventLevel.Debug));
    }

    private static async Task SeedDefaultsAsync(
        ISystemSettingRepository repository,
        CancellationToken cancellationToken)
    {
        await repository.UpsertAsync(new SystemSetting
        {
            Name = SystemSettingsService.MaxConcurrentUploadsName,
            Value = "4",
            Description = "Maximum number of concurrent uploads."
        }, cancellationToken);
        await repository.UpsertAsync(new SystemSetting
        {
            Name = SystemSettingsService.MaxConcurrentDownloadsName,
            Value = "20",
            Description = "Maximum number of concurrent downloads."
        }, cancellationToken);
        await repository.UpsertAsync(new SystemSetting
        {
            Name = SystemSettingsService.UploadBytesPerSecondName,
            Value = (50L * 1024 * 1024).ToString(),
            Description = "Total upload speed limit in bytes per second."
        }, cancellationToken);
        await repository.UpsertAsync(new SystemSetting
        {
            Name = SystemSettingsService.DownloadBytesPerSecondName,
            Value = (100L * 1024 * 1024).ToString(),
            Description = "Total download speed limit in bytes per second."
        }, cancellationToken);
        await repository.UpsertAsync(new SystemSetting
        {
            Name = SystemSettingsService.MultipartMinPartSizeBytesName,
            Value = (5L * 1024 * 1024).ToString(),
            Description = "Minimum multipart upload part size in bytes."
        }, cancellationToken);
        await repository.UpsertAsync(new SystemSetting
        {
            Name = SystemSettingsService.MultipartMaxPartSizeBytesName,
            Value = (100L * 1024 * 1024).ToString(),
            Description = "Maximum multipart upload part size in bytes."
        }, cancellationToken);
        await repository.UpsertAsync(new SystemSetting
        {
            Name = SystemSettingsService.CleanupEnabledName,
            Value = "true",
            Description = "Enables cleanup."
        }, cancellationToken);
        await repository.UpsertAsync(new SystemSetting
        {
            Name = SystemSettingsService.CleanupCompletedTaskRetentionDaysName,
            Value = "30",
            Description = "Task retention."
        }, cancellationToken);
        await repository.UpsertAsync(new SystemSetting
        {
            Name = SystemSettingsService.CleanupMultipartUploadSessionRetentionDaysName,
            Value = "30",
            Description = "Multipart retention."
        }, cancellationToken);
        await repository.UpsertAsync(new SystemSetting
        {
            Name = SystemSettingsService.CleanupInitialDelayName,
            Value = "00:05:00",
            Description = "Initial delay."
        }, cancellationToken);
        await repository.UpsertAsync(new SystemSetting
        {
            Name = SystemSettingsService.CleanupIntervalName,
            Value = "1.00:00:00",
            Description = "Interval."
        }, cancellationToken);
        await repository.UpsertAsync(new SystemSetting
        {
            Name = SystemSettingsService.LoggingLogLevelDefaultName,
            Value = "Debug",
            Description = "Log level."
        }, cancellationToken);
    }

    private sealed class AdminCurrentTenantAccessor : ICurrentTenantAccessor
    {
        public CurrentTenantContext? Current { get; set; } = new()
        {
            IsAdmin = true
        };

        public CurrentTenantContext GetRequired()
        {
            return Current ?? throw new UnauthorizedAccessException("Current tenant is required.");
        }
    }

    private sealed class InMemorySystemSettingRepository : ISystemSettingRepository
    {
        private readonly Dictionary<string, SystemSetting> _settings = new(StringComparer.OrdinalIgnoreCase);

        public Task<IReadOnlyList<SystemSetting>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<SystemSetting>>(_settings.Values.ToArray());
        }

        public Task<SystemSetting?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
        {
            _settings.TryGetValue(name, out var setting);
            return Task.FromResult(setting);
        }

        public Task UpsertAsync(SystemSetting setting, CancellationToken cancellationToken = default)
        {
            if (_settings.TryGetValue(setting.Name, out var existing))
            {
                existing.Value = setting.Value;
                existing.Description = setting.Description;
                existing.MarkUpdated();
            }
            else
            {
                _settings[setting.Name] = setting;
            }

            return Task.CompletedTask;
        }
    }
}
