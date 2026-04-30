namespace scp.filestorage.Common
{
    public static class FileStoragePathBuilder
    {
        public static string BuildStorageRelativePath(string sha256, string fileName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(sha256);
            ArgumentNullException.ThrowIfNull(fileName);

            var extension = Path.GetExtension(fileName);
            var level1 = sha256[..2];
            var level2 = sha256.Substring(2, 2);

            return Path.Combine(level1, level2, $"{sha256}{extension}");
        }
    }
}
