using scp.filestorage.Data.Dto;
using scp.filestorage.Services;

namespace SCP.StorageFSC.Services
{
    public sealed class SystemSettingsRuntimeOptions
    {
        private MultipartSettingOptions _multipart = new();
        private FileStorageCleanupOptions _cleanup = new();

        public MultipartSettingOptions Multipart => Volatile.Read(ref _multipart);

        public FileStorageCleanupOptions Cleanup => Volatile.Read(ref _cleanup);

        public void UpdateMultipart(MultipartSettingOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);
            Volatile.Write(ref _multipart, options);
        }

        public void UpdateCleanup(FileStorageCleanupOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);
            Volatile.Write(ref _cleanup, options);
        }
    }
}
