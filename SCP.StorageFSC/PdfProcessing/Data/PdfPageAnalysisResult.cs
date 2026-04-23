namespace scp.filestorage.PdfProcessing.Data
{
    public sealed class PdfPageAnalysisResult
    {
        public required int PageIndex { get; init; }
        public required int PageNumber { get; init; }

        public required int WidthPoints { get; init; }
        public required int HeightPoints { get; init; }

        public bool HasText { get; init; }
        public int CharacterCount { get; init; }
        public int NonWhitespaceCharacterCount { get; init; }
        public int WordLikeTokenCount { get; init; }

        /// <summary>
        /// Not "exactly embedded images in PDF", but a practical feature:
        /// the page looks like raster/scanned visual content.
        /// </summary>
        public bool HasImageLikeContent { get; init; }

        /// <summary>
        /// The page has visible content after rendering.
        /// </summary>
        public bool HasVisibleContent { get; init; }

        /// <summary>
        /// 0..1, the higher the value, the more the page is "inked" with non-white pixels.
        /// </summary>
        public double InkCoverage { get; init; }

        /// <summary>
        /// 0..1, the higher the value, the more shades/variability.
        /// For scans and photos, this is usually higher than for a blank page.
        /// </summary>
        public double VisualComplexity { get; init; }

        /// <summary>
        /// Practical flag: the page looks like a scan/image and is less suitable
        /// for text-oriented processing.
        /// </summary>
        public bool LooksLikeScannedPage { get; init; }

        public string? ExtractedTextPreview { get; init; }
    }
}
