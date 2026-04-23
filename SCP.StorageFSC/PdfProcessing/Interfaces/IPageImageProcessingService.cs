using scp.filestorage.PdfProcessing.Data;

namespace scp.filestorage.PdfProcessing.Interfaces
{
    public interface IPageImageProcessingService
    {
        Task<ProcessedImageResult> ProcessAsync(
            RasterPageImage input,
            PdfTransformOptions options,
            CancellationToken cancellationToken = default);
    }
}
