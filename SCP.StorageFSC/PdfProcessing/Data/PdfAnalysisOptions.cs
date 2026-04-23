namespace scp.filestorage.PdfProcessing.Data
{
    public sealed class PdfAnalysisOptions
    {
        /// <summary>
        /// DPI for visual content analysis.
        /// 72-110 is sufficient for analysis.
        /// </summary>
        public int AnalysisDpi { get; set; } = 96;

        /// <summary>
        /// Number of characters to keep in the preview.
        /// </summary>
        public int TextPreviewLength { get; set; } = 250;

        /// <summary>
        /// If the proportion of non-white pixels is above the threshold, the page is considered not empty.
        /// </summary>
        public double MinInkCoverageForVisibleContent { get; set; } = 0.0025;

        /// <summary>
        /// If there is visual content and no text, the page is considered image-like.
        /// </summary>
        public double MinInkCoverageForImageLikePage { get; set; } = 0.01;

        /// <summary>
        /// If the visual complexity is above the threshold, the page is considered scan/photo-like.
        /// </summary>
        public double MinVisualComplexityForImageLikePage { get; set; } = 0.03;
    }
}
