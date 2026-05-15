namespace scp.filestorage.Data.Dto
{
    public sealed class MultipartSettingOptions
    {
        public long MinPartSizeBytes { get; set; } = 5 * 1024 * 1024;
        public long MaxPartSizeBytes { get; set; } = 100 * 1024 * 1024;
    }
}
