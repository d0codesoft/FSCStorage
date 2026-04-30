namespace scp.filestorage.Services
{
    public interface IMultipartUploadBackgroundTaskProcessor
    {
        Task MergePartsAsync(
            Guid uploadId,
            CancellationToken cancellationToken = default);
    }
}
