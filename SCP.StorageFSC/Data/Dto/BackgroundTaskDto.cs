using SCP.StorageFSC.Data.Models;

namespace SCP.StorageFSC.Data.Dto
{
    public sealed record BackgroundTaskDto(
        Guid TaskId,
        short Type,
        string TypeName,
        BackgroundTaskStatus Status,
        string StatusName,
        Guid? UploadId,
        DateTime QueuedAtUtc,
        DateTime? StartedAtUtc,
        DateTime? CompletedAtUtc,
        DateTime? FailedAtUtc,
        string? ErrorMessage,
        string? ResultSummary);
}
