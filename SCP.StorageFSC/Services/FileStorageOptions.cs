namespace SCP.StorageFSC.Services
{
    public sealed class FileStorageOptions
    {
        public string BasePath { get; set; } = "/opt/fcs.storage/files";
        public string LogsPath { get; set; } = "/opt/fcs.storage/files/logs";
        public string DataPath { get; set; } = "/opt/fcs.storage/files/data";
    }
}
