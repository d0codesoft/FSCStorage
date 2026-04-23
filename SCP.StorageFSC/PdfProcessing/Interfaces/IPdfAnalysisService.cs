using scp.filestorage.PdfProcessing.Data;

namespace scp.filestorage.PdfProcessing.Interfaces
{
    public interface IPdfAnalysisService
    {
        Task<PdfDocumentAnalysisResult> AnalyzeAsync(
            string pdfPath,
            PdfAnalysisOptions? options = null,
            CancellationToken cancellationToken = default);

        Task<bool> PageHasTextAsync(
            string pdfPath,
            int pageIndex,
            CancellationToken cancellationToken = default);

        Task<bool> PageHasImageLikeContentAsync(
            string pdfPath,
            int pageIndex,
            PdfAnalysisOptions? options = null,
            CancellationToken cancellationToken = default);

        Task ConvertPdfAsync(
            string inputPdfPath,
            string outputPdfPath,
            PdfTransformOptions transformOptions,
            CancellationToken cancellationToken = default);
    }
}
