namespace scp.filestorage.webui.Models
{
    public sealed class BackgroundTaskViewModel
    {
        public Guid TaskId { get; set; }
        public short Type { get; set; }
        public string TypeName { get; set; } = string.Empty;
        public int Status { get; set; }
        public string StatusName { get; set; } = string.Empty;
        public Guid? UploadId { get; set; }
        public DateTime QueuedAtUtc { get; set; }
        public DateTime? StartedAtUtc { get; set; }
        public DateTime? CompletedAtUtc { get; set; }
        public DateTime? FailedAtUtc { get; set; }
        public string? ErrorMessage { get; set; }
        public string? ResultSummary { get; set; }
    }
}
