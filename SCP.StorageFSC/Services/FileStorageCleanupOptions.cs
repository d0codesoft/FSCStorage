namespace scp.filestorage.Services
{
    public sealed class FileStorageCleanupOptions
    {
        public bool Enabled { get; set; } = true;

        public int CompletedTaskRetentionDays { get; set; } = 30;

        public int MultipartUploadSessionRetentionDays { get; set; } = 30;

        public TimeSpan InitialDelay { get; set; } = TimeSpan.FromMinutes(5);

        public TimeSpan Interval { get; set; } = TimeSpan.FromDays(1);
    }
}
