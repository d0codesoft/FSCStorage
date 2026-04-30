namespace SCP.StorageFSC.Data.Models
{
    public enum BackgroundTaskStatus : short
    {
        Queued = 0,
        Running = 1,
        Completed = 2,
        Failed = 3,
        Canceled = 4
    }

    public sealed class BackgroundTask : EntityBase
    {
        public Guid TaskId { get; set; }

        public short Type { get; set; }

        public BackgroundTaskStatus Status { get; set; } = BackgroundTaskStatus.Queued;

        public Guid? UploadId { get; set; }

        public DateTime QueuedAtUtc { get; set; } = DateTime.UtcNow;

        public DateTime? StartedAtUtc { get; set; }

        public DateTime? CompletedAtUtc { get; set; }

        public DateTime? FailedAtUtc { get; set; }

        public string? ErrorMessage { get; set; }

        public string? ResultSummary { get; set; }
    }
}
