namespace SCP.StorageFSC.Data.Models
{
    /// <summary>
    /// Represents the execution status of a background task.
    /// </summary>
    public enum BackgroundTaskStatus : short
    {
        /// <summary>Task has been queued and is waiting to be picked up by the processor.</summary>
        Queued = 0,

        /// <summary>Task is currently being executed.</summary>
        Running = 1,

        /// <summary>Task completed successfully.</summary>
        Completed = 2,

        /// <summary>Task terminated due to an unhandled error.</summary>
        Failed = 3,

        /// <summary>Task was canceled before or during execution.</summary>
        Canceled = 4
    }

    /// <summary>
    /// Persistent record of a background task scheduled for asynchronous processing.
    /// </summary>
    public sealed class BackgroundTask : EntityBase
    {
        /// <summary>Unique identifier of the task instance.</summary>
        public Guid TaskId { get; set; }

        /// <summary>Numeric discriminator that maps to <see cref="FileStorageBackgroundTaskType"/>.</summary>
        public short Type { get; set; }

        /// <summary>Current lifecycle status of the task.</summary>
        public BackgroundTaskStatus Status { get; set; } = BackgroundTaskStatus.Queued;

        /// <summary>Identifier of the tenant this task belongs to, if applicable.</summary>
        public Guid? TenantId { get; set; }

        /// <summary>Human-readable description or display name for the task.</summary>
        public string Descr { get; set; } = string.Empty;

        /// <summary>Optional domain-specific value (e.g. upload ID, file ID) the task operates on.</summary>
        public Guid? ValueId { get; set; }

        /// <summary>UTC timestamp when the task was added to the queue.</summary>
        public DateTime QueuedAtUtc { get; set; } = DateTime.UtcNow;

        /// <summary>UTC timestamp when the task started execution; <c>null</c> if not yet started.</summary>
        public DateTime? StartedAtUtc { get; set; }

        /// <summary>UTC timestamp when the task finished (successfully or otherwise); <c>null</c> if still running.</summary>
        public DateTime? CompletedAtUtc { get; set; }

        /// <summary>UTC timestamp when the task failed; <c>null</c> if it has not failed.</summary>
        public DateTime? FailedAtUtc { get; set; }

        /// <summary>Error message captured when the task status is <see cref="BackgroundTaskStatus.Failed"/>.</summary>
        public string? ErrorMessage { get; set; }

        /// <summary>Short summary of the task outcome written upon completion.</summary>
        public string? ResultSummary { get; set; }
    }
}
