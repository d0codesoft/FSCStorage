public sealed class ApplicationPaths
{
    public required string BasePath { get; init; }
    public required string LogsPath { get; init; }
    public required string DataPath { get; init; }
    public required string TempPath { get; init; }

    public static ApplicationPaths FromConfiguration(
        IConfiguration configuration,
        string? rootPath = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var basePath = ResolvePath(
            rootPath ?? configuration["Paths:BasePath"],
            "storage",
            null);

        return new ApplicationPaths
        {
            BasePath = basePath,
            LogsPath = ResolvePath(configuration["Paths:LogsPath"], "{Root}/logs", basePath),
            DataPath = ResolvePath(configuration["Paths:DataPath"], "{Root}/data", basePath),
            TempPath = ResolvePath(configuration["Paths:TempPath"], "{Root}/temp", basePath)
        };
    }

    private static string ResolvePath(
        string? configuredPath,
        string defaultPath,
        string? rootPath)
    {
        var path = string.IsNullOrWhiteSpace(configuredPath)
            ? defaultPath
            : configuredPath;

        path = ExpandTokens(path, rootPath);

        path = path
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar);

        if (!Path.IsPathRooted(path))
        {
            path = Path.Combine(AppContext.BaseDirectory, path);
        }

        path = Path.GetFullPath(path);

        EnsureDirectoryExists(path);

        return path;
    }

    public static string ResolveTemplatePath(string template, string basePath, string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(template);
        ArgumentException.ThrowIfNullOrWhiteSpace(basePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        if (!template.StartsWith(token, StringComparison.OrdinalIgnoreCase))
            return Path.GetFullPath(template);

        var relativePart = template[token.Length..]
            .TrimStart('\\', '/');

        return Path.GetFullPath(Path.Combine(basePath, relativePart));
    }

    private static string ExpandTokens(string path, string? rootPath)
    {
        if (path.StartsWith("{Root}", StringComparison.OrdinalIgnoreCase) 
            && !string.IsNullOrWhiteSpace(rootPath))
        {
            path = ResolveTemplatePath(path, rootPath, "{Root}");
        }

        if (path.StartsWith("{CommonApplicationData}", StringComparison.OrdinalIgnoreCase))
        {
            path = ResolveTemplatePath(path, Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "{CommonApplicationData}");
        }

        if (path.StartsWith("{LocalApplicationData}", StringComparison.OrdinalIgnoreCase))
        {
            path = ResolveTemplatePath(path, Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "{LocalApplicationData}");
        }

        if (path.StartsWith("{LocalApplicationData}", StringComparison.OrdinalIgnoreCase))
        {
            path = ResolveTemplatePath(path, Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "{LocalApplicationData}");
        }

        if (path.StartsWith("{ApplicationData}", StringComparison.OrdinalIgnoreCase))
        {
            path = ResolveTemplatePath(path, Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "{ApplicationData}");
        }

        if (path.StartsWith("{MyDocuments}", StringComparison.OrdinalIgnoreCase))
        {
            path = ResolveTemplatePath(path, Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "{MyDocuments}");    
        }

        return path;
    }

    private static void EnsureDirectoryExists(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
        }
        catch (Exception ex) when (
            ex is IOException ||
            ex is UnauthorizedAccessException ||
            ex is NotSupportedException)
        {
            throw new InvalidOperationException(
                $"Failed to create directory: {path}",
                ex);
        }
    }
}