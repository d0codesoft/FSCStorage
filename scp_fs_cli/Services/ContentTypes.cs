namespace scp_fs_cli.Services
{
    public static class ContentTypes
    {
        public static string Get(string extension)
        {
            return extension.ToLowerInvariant() switch
            {
                ".txt" => "text/plain",
                ".json" => "application/json",
                ".pdf" => "application/pdf",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".zip" => "application/zip",
                ".xml" => "application/xml",
                _ => "application/octet-stream"
            };
        }
    }
}
