using SCP.StorageFSC.Data.Dto;
using SCP.StorageFSC.Data.Models;
using SCP.StorageFSC.Data.Repositories;
using SCP.StorageFSC.InterfacesService;
using SCP.StorageFSC.Security;
using scp.filestorage.Services;
using Serilog.Core;
using Serilog.Events;
using System.Globalization;

namespace SCP.StorageFSC.Services
{
    public sealed class SystemSettingsService : ISystemSettingsService
    {
        public const string MaxConcurrentUploadsName = "FileTransferLimiter.MaxConcurrentUploads";
        public const string MaxConcurrentDownloadsName = "FileTransferLimiter.MaxConcurrentDownloads";
        public const string UploadBytesPerSecondName = "FileTransferLimiter.UploadBytesPerSecond";
        public const string DownloadBytesPerSecondName = "FileTransferLimiter.DownloadBytesPerSecond";
        public const string MultipartMinPartSizeBytesName = "MultipartSetting.MinPartSizeBytes";
        public const string MultipartMaxPartSizeBytesName = "MultipartSetting.MaxPartSizeBytes";
        public const string CleanupEnabledName = "FileStorageCleanup.Enabled";
        public const string CleanupCompletedTaskRetentionDaysName = "FileStorageCleanup.CompletedTaskRetentionDays";
        public const string CleanupMultipartUploadSessionRetentionDaysName = "FileStorageCleanup.MultipartUploadSessionRetentionDays";
        public const string CleanupInitialDelayName = "FileStorageCleanup.InitialDelay";
        public const string CleanupIntervalName = "FileStorageCleanup.Interval";
        public const string LoggingLogLevelDefaultName = "Logging.LogLevel.Default";

        private static readonly IReadOnlyDictionary<string, SettingDefinition> Definitions =
            new Dictionary<string, SettingDefinition>(StringComparer.OrdinalIgnoreCase)
            {
                [MaxConcurrentUploadsName] = new(
                    MaxConcurrentUploadsName,
                    "Maximum number of concurrent uploads.",
                    "4",
                    ValueKind.PositiveInt),
                [MaxConcurrentDownloadsName] = new(
                    MaxConcurrentDownloadsName,
                    "Maximum number of concurrent downloads.",
                    "20",
                    ValueKind.PositiveInt),
                [UploadBytesPerSecondName] = new(
                    UploadBytesPerSecondName,
                    "Total upload speed limit in bytes per second.",
                    (50L * 1024 * 1024).ToString(CultureInfo.InvariantCulture),
                    ValueKind.PositiveRate),
                [DownloadBytesPerSecondName] = new(
                    DownloadBytesPerSecondName,
                    "Total download speed limit in bytes per second.",
                    (100L * 1024 * 1024).ToString(CultureInfo.InvariantCulture),
                    ValueKind.PositiveRate),
                [MultipartMinPartSizeBytesName] = new(
                    MultipartMinPartSizeBytesName,
                    "Minimum multipart upload part size in bytes.",
                    (5L * 1024 * 1024).ToString(CultureInfo.InvariantCulture),
                    ValueKind.PositiveLong),
                [MultipartMaxPartSizeBytesName] = new(
                    MultipartMaxPartSizeBytesName,
                    "Maximum multipart upload part size in bytes.",
                    (100L * 1024 * 1024).ToString(CultureInfo.InvariantCulture),
                    ValueKind.PositiveLong),
                [CleanupEnabledName] = new(
                    CleanupEnabledName,
                    "Enables background cleanup of completed tasks and terminal multipart sessions.",
                    "true",
                    ValueKind.Boolean),
                [CleanupCompletedTaskRetentionDaysName] = new(
                    CleanupCompletedTaskRetentionDaysName,
                    "Retention period for completed background tasks in days.",
                    "30",
                    ValueKind.PositiveInt),
                [CleanupMultipartUploadSessionRetentionDaysName] = new(
                    CleanupMultipartUploadSessionRetentionDaysName,
                    "Retention period for terminal multipart upload sessions in days.",
                    "30",
                    ValueKind.PositiveInt),
                [CleanupInitialDelayName] = new(
                    CleanupInitialDelayName,
                    "Initial delay before the cleanup worker starts.",
                    "00:05:00",
                    ValueKind.TimeSpan),
                [CleanupIntervalName] = new(
                    CleanupIntervalName,
                    "Interval between cleanup worker runs.",
                    "1.00:00:00",
                    ValueKind.TimeSpan),
                [LoggingLogLevelDefaultName] = new(
                    LoggingLogLevelDefaultName,
                    "Default application log level.",
                    "Debug",
                    ValueKind.LogLevel)
            };

        private readonly ISystemSettingRepository _repository;
        private readonly ICurrentTenantAccessor _currentTenantAccessor;
        private readonly FileTransferLimiter _transferLimiter;
        private readonly SystemSettingsRuntimeOptions _runtimeOptions;
        private readonly LoggingLevelSwitch _loggingLevelSwitch;

        public SystemSettingsService(
            ISystemSettingRepository repository,
            ICurrentTenantAccessor currentTenantAccessor,
            FileTransferLimiter transferLimiter,
            SystemSettingsRuntimeOptions runtimeOptions,
            LoggingLevelSwitch loggingLevelSwitch)
        {
            _repository = repository;
            _currentTenantAccessor = currentTenantAccessor;
            _transferLimiter = transferLimiter;
            _runtimeOptions = runtimeOptions;
            _loggingLevelSwitch = loggingLevelSwitch;
        }

        public async Task<IReadOnlyList<SystemSettingDto>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            DemandAdmin();
            await EnsureKnownSettingsAsync(cancellationToken);

            var settings = await _repository.GetAllAsync(cancellationToken);
            return settings
                .Where(setting => Definitions.ContainsKey(setting.Name))
                .Select(Map)
                .ToArray();
        }

        public async Task<SystemSettingDto?> GetByNameAsync(
            string name,
            CancellationToken cancellationToken = default)
        {
            DemandAdmin();
            var definition = GetDefinitionOrNull(name);
            if (definition is null)
                return null;

            await EnsureKnownSettingsAsync(cancellationToken);
            var setting = await _repository.GetByNameAsync(definition.Name, cancellationToken);
            return setting is null ? null : Map(setting);
        }

        public async Task<SystemSettingDto> UpdateAsync(
            string name,
            UpdateSystemSettingRequest request,
            CancellationToken cancellationToken = default)
        {
            DemandAdmin();
            ArgumentNullException.ThrowIfNull(request);

            var definition = GetDefinitionOrThrow(name);
            var normalizedValue = ValidateValue(definition, request.Value);

            var setting = new SystemSetting
            {
                Name = definition.Name,
                Value = normalizedValue,
                Description = definition.Description
            };

            await _repository.UpsertAsync(setting, cancellationToken);
            await ApplyRuntimeSettingsAsync(cancellationToken);

            var saved = await _repository.GetByNameAsync(definition.Name, cancellationToken);
            return Map(saved ?? setting);
        }

        public Task LoadFileTransferLimiterSettingsAsync(CancellationToken cancellationToken = default)
        {
            return ApplyRuntimeSettingsAsync(cancellationToken);
        }

        private async Task EnsureKnownSettingsAsync(CancellationToken cancellationToken)
        {
            foreach (var definition in Definitions.Values)
            {
                var existing = await _repository.GetByNameAsync(definition.Name, cancellationToken);
                if (existing is not null)
                    continue;

                await _repository.UpsertAsync(new SystemSetting
                {
                    Name = definition.Name,
                    Value = definition.DefaultValue,
                    Description = definition.Description
                }, cancellationToken);
            }
        }

        private async Task ApplyRuntimeSettingsAsync(CancellationToken cancellationToken)
        {
            var settings = await _repository.GetAllAsync(cancellationToken);
            var values = settings.ToDictionary(
                setting => setting.Name,
                setting => setting.Value,
                StringComparer.OrdinalIgnoreCase);

            _transferLimiter.Update(new FileTransferLimitSettings(
                ReadInt(values, MaxConcurrentUploadsName),
                ReadInt(values, MaxConcurrentDownloadsName),
                ReadLong(values, UploadBytesPerSecondName),
                ReadLong(values, DownloadBytesPerSecondName)));

            _runtimeOptions.UpdateMultipart(new scp.filestorage.Data.Dto.MultipartSettingOptions
            {
                MinPartSizeBytes = ReadLong(values, MultipartMinPartSizeBytesName),
                MaxPartSizeBytes = ReadLong(values, MultipartMaxPartSizeBytesName)
            });

            _runtimeOptions.UpdateCleanup(new FileStorageCleanupOptions
            {
                Enabled = ReadBool(values, CleanupEnabledName),
                CompletedTaskRetentionDays = ReadInt(values, CleanupCompletedTaskRetentionDaysName),
                MultipartUploadSessionRetentionDays = ReadInt(values, CleanupMultipartUploadSessionRetentionDaysName),
                InitialDelay = ReadTimeSpan(values, CleanupInitialDelayName),
                Interval = ReadTimeSpan(values, CleanupIntervalName)
            });

            _loggingLevelSwitch.MinimumLevel = ReadLogLevel(values, LoggingLogLevelDefaultName);
        }

        private static int ReadInt(IReadOnlyDictionary<string, string> values, string name)
        {
            var definition = Definitions[name];
            var value = values.TryGetValue(name, out var stored)
                ? stored
                : definition.DefaultValue;

            return int.Parse(value, CultureInfo.InvariantCulture);
        }

        private static long ReadLong(IReadOnlyDictionary<string, string> values, string name)
        {
            var definition = Definitions[name];
            var value = values.TryGetValue(name, out var stored)
                ? stored
                : definition.DefaultValue;

            return long.Parse(value, CultureInfo.InvariantCulture);
        }

        private static bool ReadBool(IReadOnlyDictionary<string, string> values, string name)
        {
            var definition = Definitions[name];
            var value = values.TryGetValue(name, out var stored)
                ? stored
                : definition.DefaultValue;

            return bool.Parse(value);
        }

        private static TimeSpan ReadTimeSpan(IReadOnlyDictionary<string, string> values, string name)
        {
            var definition = Definitions[name];
            var value = values.TryGetValue(name, out var stored)
                ? stored
                : definition.DefaultValue;

            return TimeSpan.Parse(value, CultureInfo.InvariantCulture);
        }

        private static LogEventLevel ReadLogLevel(IReadOnlyDictionary<string, string> values, string name)
        {
            var definition = Definitions[name];
            var value = values.TryGetValue(name, out var stored)
                ? stored
                : definition.DefaultValue;

            return Enum.Parse<LogEventLevel>(value, ignoreCase: true);
        }

        private void DemandAdmin()
        {
            var current = _currentTenantAccessor.GetRequired();
            if (!current.IsAdmin)
                throw new UnauthorizedAccessException("Administrative user is required.");
        }

        private static SettingDefinition GetDefinitionOrThrow(string name)
        {
            return GetDefinitionOrNull(name)
                ?? throw new ArgumentException($"Unknown system setting '{name}'.", nameof(name));
        }

        private static SettingDefinition? GetDefinitionOrNull(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return null;

            return Definitions.TryGetValue(name, out var definition)
                ? definition
                : null;
        }

        private static string ValidateValue(SettingDefinition definition, string value)
        {
            if (definition.Kind == ValueKind.Boolean)
            {
                if (!bool.TryParse(value, out var parsed))
                    throw new ArgumentException($"{definition.Name} must be true or false.");

                return parsed.ToString().ToLowerInvariant();
            }

            if (definition.Kind == ValueKind.TimeSpan)
            {
                if (!TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out var parsed) || parsed < TimeSpan.Zero)
                    throw new ArgumentException($"{definition.Name} must be a non-negative TimeSpan.");

                return parsed.ToString();
            }

            if (definition.Kind == ValueKind.LogLevel)
            {
                if (!Enum.TryParse<LogEventLevel>(value, ignoreCase: true, out var parsed))
                    throw new ArgumentException($"{definition.Name} must be a valid log level.");

                return parsed.ToString();
            }

            if (definition.Kind == ValueKind.PositiveInt)
            {
                if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) || parsed <= 0)
                    throw new ArgumentException($"{definition.Name} must be a positive integer.");

                return parsed.ToString(CultureInfo.InvariantCulture);
            }

            if (definition.Kind == ValueKind.PositiveLong)
            {
                if (!long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) || parsed <= 0)
                    throw new ArgumentException($"{definition.Name} must be a positive integer.");

                return parsed.ToString(CultureInfo.InvariantCulture);
            }

            if (!long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var rate) ||
                rate <= 0 ||
                rate > int.MaxValue)
            {
                throw new ArgumentException($"{definition.Name} must be between 1 and {int.MaxValue} bytes per second.");
            }

            return rate.ToString(CultureInfo.InvariantCulture);
        }

        private static SystemSettingDto Map(SystemSetting setting)
        {
            return new SystemSettingDto
            {
                Name = setting.Name,
                Value = setting.Value,
                Description = setting.Description,
                CreatedUtc = setting.CreatedUtc,
                UpdatedUtc = setting.UpdatedUtc
            };
        }

        private sealed record SettingDefinition(
            string Name,
            string Description,
            string DefaultValue,
            ValueKind Kind);

        private enum ValueKind
        {
            PositiveInt,
            PositiveLong,
            PositiveRate,
            Boolean,
            TimeSpan,
            LogLevel
        }
    }
}
