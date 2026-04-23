using scp.filestorage.PdfProcessing.Data;
using scp.filestorage.PdfProcessing.Interfaces;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace scp.filestorage.PdfProcessing
{
    public sealed class ImageSharpPageImageProcessingService : IPageImageProcessingService
    {
        public async Task<ProcessedImageResult> ProcessAsync(
            RasterPageImage input,
            PdfTransformOptions options,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(input);
            ArgumentNullException.ThrowIfNull(options);

            using Image<Bgra32> image = Image.LoadPixelData<Bgra32>(
                input.BgraBytes,
                input.WidthPixels,
                input.HeightPixels);

            image.Mutate(ctx =>
            {
                if (options.ConvertToGrayscale)
                {
                    ctx.Grayscale();
                }

                if (options.MaxWidth.HasValue || options.MaxHeight.HasValue)
                {
                    var (newWidth, newHeight) = CalculateFitSize(
                        image.Width,
                        image.Height,
                        options.MaxWidth,
                        options.MaxHeight);

                    if (newWidth != image.Width || newHeight != image.Height)
                    {
                        ctx.Resize(newWidth, newHeight);
                    }
                }
            });

            await using var ms = new MemoryStream();

            await image.SaveAsJpegAsync(ms, new JpegEncoder
            {
                Quality = options.JpegQuality
            }, cancellationToken);

            return new ProcessedImageResult
            {
                JpegBytes = ms.ToArray(),
                WidthPixels = image.Width,
                HeightPixels = image.Height,
                PageWidthPoints = input.PageWidthPoints,
                PageHeightPoints = input.PageHeightPoints
            };
        }

        private static (int width, int height) CalculateFitSize(
            int sourceWidth,
            int sourceHeight,
            int? maxWidth,
            int? maxHeight)
        {
            if (!maxWidth.HasValue && !maxHeight.HasValue)
                return (sourceWidth, sourceHeight);

            double scaleX = maxWidth.HasValue
                ? (double)maxWidth.Value / sourceWidth
                : double.PositiveInfinity;

            double scaleY = maxHeight.HasValue
                ? (double)maxHeight.Value / sourceHeight
                : double.PositiveInfinity;

            double scale = Math.Min(scaleX, scaleY);

            if (double.IsInfinity(scale) || scale >= 1d)
                return (sourceWidth, sourceHeight);

            return (
                Math.Max(1, (int)Math.Round(sourceWidth * scale)),
                Math.Max(1, (int)Math.Round(sourceHeight * scale))
            );
        }
    }
}
