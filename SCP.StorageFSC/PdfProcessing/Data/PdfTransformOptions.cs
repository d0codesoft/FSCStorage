namespace scp.filestorage.PdfProcessing.Data
{
    public sealed class PdfTransformOptions
    {
        public int TargetDpi { get; set; } = 150;
        public bool ConvertToGrayscale { get; set; } = true;
        public int JpegQuality { get; set; } = 70;

        /// <summary>
        /// If true, pages with noticeable text can be skipped,
        /// to avoid degrading the quality of a "normal" text PDF.
        /// </summary>
        public bool SkipPagesWithText { get; set; } = true;

        /// <summary>
        /// If true, only image-like/scanned-like pages will be processed.
        /// </summary>
        public bool ProcessOnlyImageLikePages { get; set; } = true;

        /// <summary>
        /// Additional restriction on the size of the resulting raster image.
        /// Usually null/null and TargetDpi is sufficient.
        /// </summary>
        public int? MaxWidth { get; set; }
        public int? MaxHeight { get; set; }
    }
}
