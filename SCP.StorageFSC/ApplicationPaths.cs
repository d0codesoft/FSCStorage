namespace SCP.StorageFSC
{
    public sealed class ApplicationPaths
    {
        public required string BasePath { get; init; }
        public required string LogsPath { get; init; }
        public required string DataPath { get; init; }
        public required string MultipartTempPath { get; init; }

        public static ApplicationPaths FromConfiguration(IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(configuration);

            var value = new ApplicationPaths
            {
                BasePath = ResolveConfiguredPath(configuration["Paths:BasePath"], "storage"),
                LogsPath = ResolveConfiguredPath(configuration["Paths:LogsPath"], "logs"),
                DataPath = ResolveConfiguredPath(configuration["Paths:DataPath"], "data"),
                MultipartTempPath = ResolveConfiguredPath(Path.Combine(ResolveConfiguredPath(configuration["Paths:DataPath"], "data"), "_multipart"), "_multipart")
            };
            return value;
        }

        private static string ResolveConfiguredPath(string? configuredPath, string defaultFolderName)
        {
            var resultPath = string.Empty;
            if (string.IsNullOrWhiteSpace(configuredPath))
                resultPath= Path.Combine(AppContext.BaseDirectory, defaultFolderName);
            else
                resultPath = Path.IsPathRooted(configuredPath)
                ? configuredPath
                : Path.Combine(AppContext.BaseDirectory, configuredPath);

            if (!Directory.Exists(resultPath))
            {
                try
                {
                    Directory.CreateDirectory(resultPath);
                }
                catch {
                    throw new InvalidOperationException("Failed to create directory at path: " + resultPath);
                }
            }

            return Path.GetFullPath(resultPath);
        }
    }
}
