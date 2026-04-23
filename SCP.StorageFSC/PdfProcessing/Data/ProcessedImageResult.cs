namespace scp.filestorage.PdfProcessing.Data
{
    public sealed class ProcessedImageResult
    {
        public required byte[] JpegBytes { get; init; }
        public required int WidthPixels { get; init; }
        public required int HeightPixels { get; init; }

        public required int PageWidthPoints { get; init; }
        public required int PageHeightPoints { get; init; }
    }
}
