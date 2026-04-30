namespace scp.filestorage.Services
{
    public interface IFileStorageConsistencyService
    {
        Task<FileStorageConsistencyCheckResult> CheckAsync(
            CancellationToken cancellationToken = default);
    }

    public sealed record FileStorageConsistencyCheckResult(
        DateTime CheckedAtUtc,
        int CheckedFiles,
        int MissingFiles,
        int SizeMismatches,
        int HashMismatches,
        int ReferenceCountMismatches,
        int OrphanFiles,
        IReadOnlyList<FileStorageConsistencyIssue> Issues)
    {
        public bool IsConsistent => Issues.Count == 0;
    }

    public sealed record FileStorageConsistencyIssue(
        Guid StoredFileId,
        string Code,
        string Message,
        string? PhysicalPath);
}
