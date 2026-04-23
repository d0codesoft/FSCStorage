namespace scp.filestorage.PdfProcessing.Data
{
    public sealed class PdfDocumentAnalysisResult
    {
        public required string SourcePath { get; init; }
        public required int PageCount { get; init; }
        public required IReadOnlyList<PdfPageAnalysisResult> Pages { get; init; }

        public bool HasAnyText => Pages.Any(p => p.HasText);
        public bool HasAnyImageLikeContent => Pages.Any(p => p.HasImageLikeContent);
        public bool LooksMostlyScanned => Pages.Count > 0 && Pages.Count(p => p.LooksLikeScannedPage) >= Math.Ceiling(Pages.Count * 0.6);

        public bool IsGoodCandidateForRasterGrayscaleConversion =>
            Pages.Count > 0 &&
            Pages.All(p => !p.HasText || p.LooksLikeScannedPage);

        public string BuildSummary()
        {
            var textPages = Pages.Count(p => p.HasText);
            var imageLikePages = Pages.Count(p => p.HasImageLikeContent);
            var scannedPages = Pages.Count(p => p.LooksLikeScannedPage);

            return $"Pages={PageCount}, TextPages={textPages}, ImageLikePages={imageLikePages}, ScannedLikePages={scannedPages}, LooksMostlyScanned={LooksMostlyScanned}, GoodCandidate={IsGoodCandidateForRasterGrayscaleConversion}";
        }
    }
}
