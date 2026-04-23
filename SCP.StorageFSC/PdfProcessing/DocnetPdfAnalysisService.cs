using Docnet.Core;
using Docnet.Core.Models;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using scp.filestorage.PdfProcessing.Data;
using scp.filestorage.PdfProcessing.Interfaces;
using System.Text.RegularExpressions;

namespace scp.filestorage.PdfProcessing
{
    public sealed class DocnetPdfAnalysisService : IPdfAnalysisService
    {
        private static readonly SemaphoreSlim PdfiumLock = new(1, 1);

        private readonly IPageImageProcessingService _imageProcessingService;

        public DocnetPdfAnalysisService(IPageImageProcessingService imageProcessingService)
        {
            _imageProcessingService = imageProcessingService;
        }

        public async Task<PdfDocumentAnalysisResult> AnalyzeAsync(
            string pdfPath,
            PdfAnalysisOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            options ??= new PdfAnalysisOptions();
            ValidateInputFile(pdfPath);

            await PdfiumLock.WaitAsync(cancellationToken);
            try
            {
                var pdfBytes = await File.ReadAllBytesAsync(pdfPath, cancellationToken);

                using var docReader = DocLib.Instance.GetDocReader(pdfBytes, new PageDimensions(1, 1));
                int pageCount = docReader.GetPageCount();

                var pages = new List<PdfPageAnalysisResult>(pageCount);

                for (int pageIndex = 0; pageIndex < pageCount; pageIndex++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var page = AnalyzePageInternal(pdfBytes, pageIndex, options);
                    pages.Add(page);
                }

                return new PdfDocumentAnalysisResult
                {
                    SourcePath = pdfPath,
                    PageCount = pageCount,
                    Pages = pages
                };
            }
            finally
            {
                PdfiumLock.Release();
            }
        }

        public async Task<bool> PageHasTextAsync(
            string pdfPath,
            int pageIndex,
            CancellationToken cancellationToken = default)
        {
            var result = await AnalyzeAsync(
                pdfPath,
                new PdfAnalysisOptions(),
                cancellationToken);

            ValidatePageIndex(pageIndex, result.PageCount);
            return result.Pages[pageIndex].HasText;
        }

        public async Task<bool> PageHasImageLikeContentAsync(
            string pdfPath,
            int pageIndex,
            PdfAnalysisOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            options ??= new PdfAnalysisOptions();

            var result = await AnalyzeAsync(pdfPath, options, cancellationToken);
            ValidatePageIndex(pageIndex, result.PageCount);
            return result.Pages[pageIndex].HasImageLikeContent;
        }

        public async Task ConvertPdfAsync(
            string inputPdfPath,
            string outputPdfPath,
            PdfTransformOptions transformOptions,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(transformOptions);
            ValidateInputFile(inputPdfPath);

            var analysis = await AnalyzeAsync(
                inputPdfPath,
                new PdfAnalysisOptions(),
                cancellationToken);

            await PdfiumLock.WaitAsync(cancellationToken);
            try
            {
                var pdfBytes = await File.ReadAllBytesAsync(inputPdfPath, cancellationToken);
                using var outPdf = new PdfDocument();

                using var pageSizeReader = DocLib.Instance.GetDocReader(pdfBytes, new PageDimensions(1, 1));

                for (int pageIndex = 0; pageIndex < analysis.PageCount; pageIndex++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var pageInfo = analysis.Pages[pageIndex];

                    bool shouldProcess = ShouldProcessPage(pageInfo, transformOptions);

                    if (!shouldProcess)
                    {
                        // Даже если не преобразуем, страница всё равно должна попасть в новый PDF.
                        // Мы её просто рендерим без grayscale и с более щадящими параметрами.
                        var passthroughOptions = new PdfTransformOptions
                        {
                            TargetDpi = transformOptions.TargetDpi,
                            ConvertToGrayscale = false,
                            JpegQuality = Math.Max(transformOptions.JpegQuality, 85),
                            SkipPagesWithText = false,
                            ProcessOnlyImageLikePages = false,
                            MaxWidth = transformOptions.MaxWidth,
                            MaxHeight = transformOptions.MaxHeight
                        };

                        var raster = RenderPageToRaster(
                            pdfBytes,
                            pageIndex,
                            pageInfo.WidthPoints,
                            pageInfo.HeightPoints,
                            transformOptions.TargetDpi);

                        var processed = await _imageProcessingService.ProcessAsync(
                            raster,
                            passthroughOptions,
                            cancellationToken);

                        AddJpegPage(outPdf, processed);
                        continue;
                    }

                    var pageRaster = RenderPageToRaster(
                        pdfBytes,
                        pageIndex,
                        pageInfo.WidthPoints,
                        pageInfo.HeightPoints,
                        transformOptions.TargetDpi);

                    var transformed = await _imageProcessingService.ProcessAsync(
                        pageRaster,
                        transformOptions,
                        cancellationToken);

                    AddJpegPage(outPdf, transformed);
                }

                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPdfPath))!);
                outPdf.Save(outputPdfPath);
            }
            finally
            {
                PdfiumLock.Release();
            }
        }

        private static bool ShouldProcessPage(PdfPageAnalysisResult page, PdfTransformOptions options)
        {
            if (options.SkipPagesWithText && page.HasText && !page.LooksLikeScannedPage)
                return false;

            if (options.ProcessOnlyImageLikePages && !page.HasImageLikeContent && !page.LooksLikeScannedPage)
                return false;

            return true;
        }

        private static PdfPageAnalysisResult AnalyzePageInternal(
            byte[] pdfBytes,
            int pageIndex,
            PdfAnalysisOptions options)
        {
            using var pageMetaReader = DocLib.Instance.GetDocReader(pdfBytes, new PageDimensions(1, 1));
            using var pageReader = pageMetaReader.GetPageReader(pageIndex);

            int widthPoints = pageReader.GetPageWidth();
            int heightPoints = pageReader.GetPageHeight();

            string text = pageReader.GetText() ?? string.Empty;
            int charCount = text.Length;
            int nonWhitespace = text.Count(c => !char.IsWhiteSpace(c));
            int wordLikeTokenCount = Regex.Matches(text, @"\p{L}[\p{L}\p{N}\-_/]*").Count;
            bool hasText = nonWhitespace > 0;

            int renderWidth = PointsToPixels(widthPoints, options.AnalysisDpi);
            int renderHeight = PointsToPixels(heightPoints, options.AnalysisDpi);

            using var imageDocReader = DocLib.Instance.GetDocReader(pdfBytes, new PageDimensions(renderWidth, renderHeight));
            using var imagePageReader = imageDocReader.GetPageReader(pageIndex);

            var rawBytes = imagePageReader.GetImage(default(RenderFlags));

            var visual = AnalyzeRasterBytes(rawBytes);

            bool hasVisibleContent = visual.InkCoverage >= options.MinInkCoverageForVisibleContent;

            bool hasImageLikeContent =
                hasVisibleContent &&
                (
                    !hasText ||
                    visual.InkCoverage >= options.MinInkCoverageForImageLikePage ||
                    visual.VisualComplexity >= options.MinVisualComplexityForImageLikePage
                );

            bool looksLikeScannedPage =
                !hasText &&
                hasVisibleContent &&
                (
                    visual.InkCoverage >= options.MinInkCoverageForImageLikePage ||
                    visual.VisualComplexity >= options.MinVisualComplexityForImageLikePage
                );

            return new PdfPageAnalysisResult
            {
                PageIndex = pageIndex,
                PageNumber = pageIndex + 1,
                WidthPoints = widthPoints,
                HeightPoints = heightPoints,
                HasText = hasText,
                CharacterCount = charCount,
                NonWhitespaceCharacterCount = nonWhitespace,
                WordLikeTokenCount = wordLikeTokenCount,
                HasVisibleContent = hasVisibleContent,
                HasImageLikeContent = hasImageLikeContent,
                InkCoverage = visual.InkCoverage,
                VisualComplexity = visual.VisualComplexity,
                LooksLikeScannedPage = looksLikeScannedPage,
                ExtractedTextPreview = string.IsNullOrWhiteSpace(text)
                    ? null
                    : text.Length <= options.TextPreviewLength
                        ? text
                        : text[..options.TextPreviewLength]
            };
        }

        private static RasterPageImage RenderPageToRaster(
            byte[] pdfBytes,
            int pageIndex,
            int pageWidthPoints,
            int pageHeightPoints,
            int targetDpi)
        {
            int pixelWidth = PointsToPixels(pageWidthPoints, targetDpi);
            int pixelHeight = PointsToPixels(pageHeightPoints, targetDpi);

            using var docReader = DocLib.Instance.GetDocReader(
                pdfBytes,
                new PageDimensions(pixelWidth, pixelHeight));

            using var pageReader = docReader.GetPageReader(pageIndex);

            // Docnet умеет сразу grayscale-рендер через RenderFlags.Grayscale,
            // но здесь держим обработку централизованно в image service.
            var rawBytes = pageReader.GetImage(default(RenderFlags));

            return new RasterPageImage
            {
                WidthPixels = pageReader.GetPageWidth(),
                HeightPixels = pageReader.GetPageHeight(),
                PageWidthPoints = pageWidthPoints,
                PageHeightPoints = pageHeightPoints,
                BgraBytes = rawBytes
            };
        }

        private static (double InkCoverage, double VisualComplexity) AnalyzeRasterBytes(byte[] bgraBytes)
        {
            if (bgraBytes.Length < 4)
                return (0d, 0d);

            int pixelCount = bgraBytes.Length / 4;
            int nonWhitePixels = 0;

            long sumGray = 0;
            long sumGraySq = 0;

            for (int i = 0; i < bgraBytes.Length; i += 4)
            {
                byte b = bgraBytes[i + 0];
                byte g = bgraBytes[i + 1];
                byte r = bgraBytes[i + 2];
                byte a = bgraBytes[i + 3];

                int gray = (r * 299 + g * 587 + b * 114) / 1000;

                // Прозрачные/почти белые считаем фоном
                bool isForeground = a > 16 && gray < 245;
                if (isForeground)
                    nonWhitePixels++;

                sumGray += gray;
                sumGraySq += (long)gray * gray;
            }

            double inkCoverage = pixelCount == 0 ? 0d : (double)nonWhitePixels / pixelCount;

            double mean = (double)sumGray / pixelCount;
            double variance = ((double)sumGraySq / pixelCount) - (mean * mean);

            if (variance < 0)
                variance = 0;

            // Нормируем грубо к диапазону 0..1
            double visualComplexity = Math.Min(1d, Math.Sqrt(variance) / 128d);

            return (inkCoverage, visualComplexity);
        }

        private static void AddJpegPage(PdfDocument pdf, ProcessedImageResult processed)
        {
            var page = pdf.AddPage();
            page.Width = processed.PageWidthPoints;
            page.Height = processed.PageHeightPoints;

            using var image = XImage.FromStream(() => new MemoryStream(processed.JpegBytes));
            using var gfx = XGraphics.FromPdfPage(page);

            gfx.DrawImage(image, 0, 0, page.Width, page.Height);
        }

        private static int PointsToPixels(int points, int dpi)
        {
            return Math.Max(1, (int)Math.Round(points / 72d * dpi));
        }

        private static void ValidateInputFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Path is required.", nameof(path));

            if (!File.Exists(path))
                throw new FileNotFoundException("PDF file not found.", path);
        }

        private static void ValidatePageIndex(int pageIndex, int pageCount)
        {
            if (pageIndex < 0 || pageIndex >= pageCount)
                throw new ArgumentOutOfRangeException(nameof(pageIndex), $"Page index must be in range 0..{pageCount - 1}");
        }
    }
}
