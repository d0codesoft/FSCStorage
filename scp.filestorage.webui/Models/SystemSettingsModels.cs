using System.ComponentModel.DataAnnotations;

namespace scp.filestorage.webui.Models
{
    public sealed class SystemSettingViewModel
    {
        public string Name { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime CreatedUtc { get; set; }
        public DateTime? UpdatedUtc { get; set; }
    }

    public sealed class UpdateSystemSettingRequestModel
    {
        public string Value { get; set; } = string.Empty;
    }

    public sealed class FileTransferSettingsEditorModel
    {
        [Range(1, int.MaxValue, ErrorMessage = "Max concurrent uploads must be greater than 0.")]
        public int MaxConcurrentUploads { get; set; } = 4;

        [Range(1, int.MaxValue, ErrorMessage = "Max concurrent downloads must be greater than 0.")]
        public int MaxConcurrentDownloads { get; set; } = 20;

        [Range(1, int.MaxValue, ErrorMessage = "Upload speed must be greater than 0.")]
        public long UploadMegabytesPerSecond { get; set; } = 50;

        [Range(1, int.MaxValue, ErrorMessage = "Download speed must be greater than 0.")]
        public long DownloadMegabytesPerSecond { get; set; } = 100;

        [Range(1, long.MaxValue, ErrorMessage = "Minimum part size must be greater than 0.")]
        public long MinPartSizeMegabytes { get; set; } = 5;

        [Range(1, long.MaxValue, ErrorMessage = "Maximum part size must be greater than 0.")]
        public long MaxPartSizeMegabytes { get; set; } = 100;

        public bool CleanupEnabled { get; set; } = true;

        [Range(1, int.MaxValue, ErrorMessage = "Completed task retention must be greater than 0.")]
        public int CompletedTaskRetentionDays { get; set; } = 30;

        [Range(1, int.MaxValue, ErrorMessage = "Multipart upload retention must be greater than 0.")]
        public int MultipartUploadSessionRetentionDays { get; set; } = 30;

        public string InitialDelay { get; set; } = "00:05:00";

        public string Interval { get; set; } = "1.00:00:00";

        public string LogLevelDefault { get; set; } = "Debug";
    }
}
