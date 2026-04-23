namespace scp.filestorage.Data.Dto
{
    public sealed class FileStorageMultipartOptions
    {
        public string RootPath { get; set; } = Path.Combine(AppContext.BaseDirectory, "storage");
        public string TempFolderName { get; set; } = "_multipart";
        public string FilesFolderName { get; set; } = "files";
        public long MinPartSizeBytes { get; set; } = 5 * 1024 * 1024;
        public long MaxPartSizeBytes { get; set; } = 100 * 1024 * 1024;
    }
}
