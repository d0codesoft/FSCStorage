namespace scp.filestorage.PdfProcessing.Data
{
    public sealed class RasterPageImage
    {
        public required int WidthPixels { get; init; }
        public required int HeightPixels { get; init; }

        /// <summary>
        /// The size of the original PDF page in points (1/72 inch).
        /// Needed to preserve the physical size of the page in the new PDF.
        /// </summary>
        public required int PageWidthPoints { get; init; }
        public required int PageHeightPoints { get; init; }

        /// <summary>
        /// BGRA raw bytes obtained from Docnet.
        /// </summary>
        public required byte[] BgraBytes { get; init; }
    }
}
