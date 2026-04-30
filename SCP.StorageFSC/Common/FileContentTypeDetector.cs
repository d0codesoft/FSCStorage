using System.Buffers;
using System.Globalization;

namespace scp.filestorage.Common
{
    public enum FileContentTypeDetectionSource
    {
        MagicBytes,
        Extension,
        TextHeuristic,
        Fallback
    }

    public sealed record FileContentTypeDetectionResult(
        string ContentType,
        string? DetectedExtension,
        FileContentTypeDetectionSource Source,
        string Reason);

    public static class FileContentTypeDetector
    {
        public const string DefaultContentType = "application/octet-stream";

        private static readonly IReadOnlyDictionary<string, string> ExtensionContentTypes =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [".txt"] = "text/plain",
                [".log"] = "text/plain",
                [".csv"] = "text/csv",
                [".json"] = "application/json",
                [".xml"] = "application/xml",
                [".html"] = "text/html",
                [".htm"] = "text/html",
                [".css"] = "text/css",
                [".js"] = "application/javascript",
                [".ts"] = "text/typescript",
                [".md"] = "text/markdown",
                [".yaml"] = "application/yaml",
                [".yml"] = "application/yaml",
                [".pdf"] = "application/pdf",
                [".jpg"] = "image/jpeg",
                [".jpeg"] = "image/jpeg",
                [".png"] = "image/png",
                [".gif"] = "image/gif",
                [".webp"] = "image/webp",
                [".bmp"] = "image/bmp",
                [".tif"] = "image/tiff",
                [".tiff"] = "image/tiff",
                [".svg"] = "image/svg+xml",
                [".zip"] = "application/zip",
                [".7z"] = "application/x-7z-compressed",
                [".rar"] = "application/vnd.rar",
                [".gz"] = "application/gzip",
                [".mp4"] = "video/mp4",
                [".mkv"] = "video/x-matroska",
                [".mov"] = "video/quicktime",
                [".webm"] = "video/webm",
                [".mp3"] = "audio/mpeg",
                [".ogg"] = "audio/ogg",
                [".wav"] = "audio/wav",
                [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                [".pptx"] = "application/vnd.openxmlformats-officedocument.presentationml.presentation"
            };

        public static FileContentTypeDetectionResult Detect(
            string? fileName,
            ReadOnlySpan<byte> header)
        {
            var magicResult = DetectByMagicBytes(header);
            if (magicResult is not null)
                return magicResult;

            var extension = GetNormalizedExtension(fileName);
            if (extension is not null &&
                ExtensionContentTypes.TryGetValue(extension, out var contentType))
            {
                return new FileContentTypeDetectionResult(
                    contentType,
                    extension,
                    FileContentTypeDetectionSource.Extension,
                    $"Content type detected by extension '{extension}'.");
            }

            if (LooksLikeText(header))
            {
                return new FileContentTypeDetectionResult(
                    "text/plain",
                    extension,
                    FileContentTypeDetectionSource.TextHeuristic,
                    "Header looks like text data.");
            }

            return new FileContentTypeDetectionResult(
                DefaultContentType,
                extension,
                FileContentTypeDetectionSource.Fallback,
                "Content type could not be detected.");
        }

        public static async Task<FileContentTypeDetectionResult> DetectAsync(
            string filePath,
            string? fileName,
            int headerSize = 4096,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

            await using var stream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: headerSize,
                useAsync: true);

            return await DetectAsync(stream, fileName, headerSize, cancellationToken);
        }

        public static async Task<FileContentTypeDetectionResult> DetectAsync(
            Stream stream,
            string? fileName,
            int headerSize = 4096,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(stream);

            var rented = ArrayPool<byte>.Shared.Rent(headerSize);

            try
            {
                var originalPosition = stream.CanSeek ? stream.Position : (long?)null;

                var read = await stream.ReadAsync(
                    rented.AsMemory(0, headerSize),
                    cancellationToken);

                if (originalPosition.HasValue)
                    stream.Position = originalPosition.Value;

                return Detect(fileName, rented.AsSpan(0, read));
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }

        private static string? GetNormalizedExtension(string? fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return null;

            var extension = Path.GetExtension(fileName);

            return string.IsNullOrWhiteSpace(extension)
                ? null
                : extension.ToLower(CultureInfo.InvariantCulture);
        }

        private static FileContentTypeDetectionResult? DetectByMagicBytes(ReadOnlySpan<byte> h)
        {
            if (h.Length >= 4 && h[..4].SequenceEqual("%PDF"u8))
                return Magic("application/pdf", ".pdf", "PDF header detected.");

            if (h.Length >= 2 && h[0] == 0xFF && h[1] == 0xD8)
                return Magic("image/jpeg", ".jpg", "JPEG header detected.");

            if (h.Length >= 8 && h[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
                return Magic("image/png", ".png", "PNG header detected.");

            if (h.Length >= 12 &&
                h[..4].SequenceEqual("RIFF"u8) &&
                h[8..12].SequenceEqual("WEBP"u8))
                return Magic("image/webp", ".webp", "WEBP header detected.");

            if (h.Length >= 6 &&
                (h[..6].SequenceEqual("GIF87a"u8) || h[..6].SequenceEqual("GIF89a"u8)))
                return Magic("image/gif", ".gif", "GIF header detected.");

            if (h.Length >= 2 && h[..2].SequenceEqual("BM"u8))
                return Magic("image/bmp", ".bmp", "BMP header detected.");

            if (h.Length >= 4 &&
                (h[..4].SequenceEqual(new byte[] { 0x49, 0x49, 0x2A, 0x00 }) ||
                 h[..4].SequenceEqual(new byte[] { 0x4D, 0x4D, 0x00, 0x2A })))
                return Magic("image/tiff", ".tiff", "TIFF header detected.");

            if (h.Length >= 4 && h[..4].SequenceEqual("PK\x03\x04"u8))
                return Magic("application/zip", ".zip", "ZIP header detected.");

            if (h.Length >= 6 && h[..6].SequenceEqual(new byte[] { 0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C }))
                return Magic("application/x-7z-compressed", ".7z", "7z header detected.");

            if (h.Length >= 7 && h[..7].SequenceEqual(new byte[] { 0x52, 0x61, 0x72, 0x21, 0x1A, 0x07, 0x00 }))
                return Magic("application/vnd.rar", ".rar", "RAR header detected.");

            if (h.Length >= 2 && h[0] == 0x1F && h[1] == 0x8B)
                return Magic("application/gzip", ".gz", "GZip header detected.");

            if (h.Length >= 12 &&
                h[4..8].SequenceEqual("ftyp"u8))
                return Magic("video/mp4", ".mp4", "ISO base media header detected.");

            return null;

            static FileContentTypeDetectionResult Magic(
                string contentType,
                string extension,
                string reason)
            {
                return new FileContentTypeDetectionResult(
                    contentType,
                    extension,
                    FileContentTypeDetectionSource.MagicBytes,
                    reason);
            }
        }

        private static bool LooksLikeText(ReadOnlySpan<byte> data)
        {
            if (data.IsEmpty)
                return false;

            var controlCharacters = 0;
            var printableCharacters = 0;
            var inspected = Math.Min(data.Length, 1024);

            for (var i = 0; i < inspected; i++)
            {
                var b = data[i];

                if (b == 0)
                    return false;

                if (b is 9 or 10 or 13)
                {
                    printableCharacters++;
                    continue;
                }

                if (b >= 32 && b <= 126)
                {
                    printableCharacters++;
                    continue;
                }

                if (b >= 0xC2)
                {
                    printableCharacters++;
                    continue;
                }

                if (b < 32)
                    controlCharacters++;
            }

            return printableCharacters > 0 &&
                   controlCharacters <= inspected * 0.05;
        }
    }
}
