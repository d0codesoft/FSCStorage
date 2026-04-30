using System.Buffers;
using System.Globalization;

namespace scp.filestorage.Common
{
    public enum FileCompressionDecision
    {
        /// <summary>
        /// Indicates that the file should be compressed.
        /// </summary>
        Compress,

        /// <summary>
        /// Indicates that compression should be skipped.
        /// </summary>
        Skip,

        /// <summary>
        /// Indicates that the file requires additional inspection before making a compression decision.
        /// </summary>
        Inspect
    }

    /// <summary>
    /// Represents the result of file compression analysis.
    /// </summary>
    /// <param name="Decision">The recommended compression decision for the file.</param>
    /// <param name="Reason">The reason why the decision was made.</param>
    /// <param name="DetectedMimeType">The detected MIME type, if available.</param>
    /// <param name="DetectedExtension">The detected or normalized file extension, if available.</param>
    /// <param name="IsAlreadyCompressed">Indicates whether the file appears to already be compressed.</param>
    public sealed record FileCompressionDetectionResult(
        FileCompressionDecision Decision,
        string Reason,
        string? DetectedMimeType,
        string? DetectedExtension,
        bool IsAlreadyCompressed
    );

    public static class FileCompressionDetector
    {
        private static readonly HashSet<string> AlwaysCompressExtensions =
        [
            ".txt", ".log", ".csv", ".json", ".xml", ".html", ".htm",
        ".css", ".js", ".ts", ".sql", ".md", ".yaml", ".yml",
        ".ini", ".conf", ".config", ".env", ".svg"
        ];

        private static readonly HashSet<string> InspectExtensions =
        [
            ".pdf", ".bmp", ".tif", ".tiff", ".rtf"
        ];

        private static readonly HashSet<string> SkipExtensions =
        [
            ".zip", ".7z", ".rar", ".gz", ".gzip", ".bz2", ".xz", ".zst",
        ".jpg", ".jpeg", ".png", ".webp", ".avif", ".heic", ".heif",
        ".mp4", ".mkv", ".mov", ".webm", ".avi",
        ".mp3", ".aac", ".ogg", ".opus", ".flac", ".m4a",
        ".docx", ".xlsx", ".pptx", ".odt", ".ods", ".odp",
        ".exe", ".dll", ".so", ".dylib"
        ];

        public static FileCompressionDetectionResult Detect(
            string? fileName,
            ReadOnlySpan<byte> header)
        {
            var extension = GetNormalizedExtension(fileName);

            var magicResult = DetectByMagicBytes(header);
            if (magicResult is not null)
                return magicResult;

            if (extension is not null)
            {
                if (AlwaysCompressExtensions.Contains(extension))
                {
                    return new FileCompressionDetectionResult(
                        FileCompressionDecision.Compress,
                        $"Extension '{extension}' is usually text-based and compresses well.",
                        null,
                        extension,
                        IsAlreadyCompressed: false);
                }

                if (InspectExtensions.Contains(extension))
                {
                    return new FileCompressionDetectionResult(
                        FileCompressionDecision.Inspect,
                        $"Extension '{extension}' needs content inspection before compression.",
                        null,
                        extension,
                        IsAlreadyCompressed: false);
                }

                if (SkipExtensions.Contains(extension))
                {
                    return new FileCompressionDetectionResult(
                        FileCompressionDecision.Skip,
                        $"Extension '{extension}' is already compressed or poorly compressible.",
                        null,
                        extension,
                        IsAlreadyCompressed: true);
                }
            }

            if (LooksLikeText(header))
            {
                return new FileCompressionDetectionResult(
                    FileCompressionDecision.Compress,
                    "Header looks like text data.",
                    "text/plain",
                    extension,
                    IsAlreadyCompressed: false);
            }

            return new FileCompressionDetectionResult(
                FileCompressionDecision.Inspect,
                "Unknown binary file. Compression benefit should be tested.",
                null,
                extension,
                IsAlreadyCompressed: false);
        }

        public static async Task<FileCompressionDetectionResult> DetectAsync(
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

        private static FileCompressionDetectionResult? DetectByMagicBytes(ReadOnlySpan<byte> h)
        {
            if (h.Length >= 4 && h[..4].SequenceEqual("%PDF"u8))
            {
                return new FileCompressionDetectionResult(
                    FileCompressionDecision.Inspect,
                    "PDF detected. Compression depends on internal content.",
                    "application/pdf",
                    ".pdf",
                    IsAlreadyCompressed: false);
            }

            if (h.Length >= 2 && h[0] == 0xFF && h[1] == 0xD8)
            {
                return Skip("image/jpeg", ".jpg", "JPEG is already compressed.");
            }

            if (h.Length >= 8 && h[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
            {
                return Skip("image/png", ".png", "PNG is already compressed.");
            }

            if (h.Length >= 12 &&
                h[..4].SequenceEqual("RIFF"u8) &&
                h[8..12].SequenceEqual("WEBP"u8))
            {
                return Skip("image/webp", ".webp", "WEBP is already compressed.");
            }

            if (h.Length >= 6 &&
                (h[..6].SequenceEqual("GIF87a"u8) || h[..6].SequenceEqual("GIF89a"u8)))
            {
                return Skip("image/gif", ".gif", "GIF is usually already compressed.");
            }

            if (h.Length >= 4 && h[..4].SequenceEqual("PK\x03\x04"u8))
            {
                return Skip("application/zip", ".zip", "ZIP-based format is already compressed.");
            }

            if (h.Length >= 6 && h[..6].SequenceEqual(new byte[] { 0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C }))
            {
                return Skip("application/x-7z-compressed", ".7z", "7z is already compressed.");
            }

            if (h.Length >= 7 && h[..7].SequenceEqual(new byte[] { 0x52, 0x61, 0x72, 0x21, 0x1A, 0x07, 0x00 }))
            {
                return Skip("application/vnd.rar", ".rar", "RAR is already compressed.");
            }

            if (h.Length >= 2 && h[0] == 0x1F && h[1] == 0x8B)
            {
                return Skip("application/gzip", ".gz", "GZip is already compressed.");
            }

            if (h.Length >= 6 && h[..6].SequenceEqual(new byte[] { 0xFD, 0x37, 0x7A, 0x58, 0x5A, 0x00 }))
            {
                return Skip("application/x-xz", ".xz", "XZ is already compressed.");
            }

            if (h.Length >= 4 && h[..4].SequenceEqual(new byte[] { 0x28, 0xB5, 0x2F, 0xFD }))
            {
                return Skip("application/zstd", ".zst", "Zstandard file is already compressed.");
            }

            if (h.Length >= 2 && h[..2].SequenceEqual("BM"u8))
            {
                return new FileCompressionDetectionResult(
                    FileCompressionDecision.Compress,
                    "BMP detected. Bitmap images often compress well.",
                    "image/bmp",
                    ".bmp",
                    IsAlreadyCompressed: false);
            }

            if (h.Length >= 4 &&
                (h[..4].SequenceEqual(new byte[] { 0x49, 0x49, 0x2A, 0x00 }) ||
                 h[..4].SequenceEqual(new byte[] { 0x4D, 0x4D, 0x00, 0x2A })))
            {
                return new FileCompressionDetectionResult(
                    FileCompressionDecision.Inspect,
                    "TIFF detected. It may already use internal compression.",
                    "image/tiff",
                    ".tiff",
                    IsAlreadyCompressed: false);
            }

            return null;

            static FileCompressionDetectionResult Skip(
                string mime,
                string extension,
                string reason)
            {
                return new FileCompressionDetectionResult(
                    FileCompressionDecision.Skip,
                    reason,
                    mime,
                    extension,
                    IsAlreadyCompressed: true);
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
