using scp.filestorage.Data.Dto;

namespace scp.filestorage.InterfacesService
{
    public interface IFileStorageMultipartService
    {
        Task<InitMultipartUploadResultDto> InitAsync(
            InitMultipartUploadRequestDto request,
            CancellationToken cancellationToken = default);

        Task<UploadMultipartPartResultDto> UploadPartAsync(
            UploadMultipartPartRequestDto request,
            CancellationToken cancellationToken = default);

        Task<MultipartUploadStatusDto?> GetStatusAsync(
            Guid uploadId,
            CancellationToken cancellationToken = default);

        Task<CompleteMultipartUploadResultDto> CompleteAsync(
            CompleteMultipartUploadRequestDto request,
            CancellationToken cancellationToken = default);

        Task<AbortMultipartUploadResultDto> AbortAsync(
            Guid uploadId,
            CancellationToken cancellationToken = default);
    }
}
