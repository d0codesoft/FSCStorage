using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Serilog;

var logPath = GetLogPath();
Log.Logger = CreateLogger(logPath);

try
{
    Log.Information("Installer tool started. Log file: {LogPath}", logPath);

    var options = InstallerOptions.Parse(args);
    LogOptions(options);

    ConfigureTransport(options);

    Log.Information("Transport configuration completed successfully.");
    return 0;
}
catch (Exception ex)
{
    Log.Fatal(ex, "Configure transport failed.");
    Console.Error.WriteLine($"Configure transport failed: {ex.Message}");
    return 1;
}
finally
{
    Log.CloseAndFlush();
}

static void ConfigureTransport(InstallerOptions options)
{
    Log.Information("Starting transport configuration in mode {Mode}.", options.Mode);

    var appSettingsPath = options.AppSettingsPath;
    if (string.IsNullOrWhiteSpace(appSettingsPath))
    {
        if (string.IsNullOrWhiteSpace(options.InstallDir))
        {
            throw new InvalidOperationException("Install directory or appsettings path is required.");
        }

        appSettingsPath = Path.Combine(options.InstallDir, "appsettings.json");
    }

    appSettingsPath = Path.GetFullPath(appSettingsPath);
    Log.Information("Using appsettings file {AppSettingsPath}.", appSettingsPath);

    if (string.IsNullOrWhiteSpace(options.GeneratedPfxPath) && !string.IsNullOrWhiteSpace(options.InstallDir))
    {
        options.GeneratedPfxPath = Path.Combine(options.InstallDir, "certs", "server.pfx");
        Log.Information("Generated PFX path was not specified. Using default path {GeneratedPfxPath}.", options.GeneratedPfxPath);
    }

    if (!File.Exists(appSettingsPath))
    {
        Log.Error("App settings file was not found at {AppSettingsPath}.", appSettingsPath);
        throw new FileNotFoundException("App settings file was not found.", appSettingsPath);
    }

    Log.Information("Loading appsettings JSON from {AppSettingsPath}.", appSettingsPath);
    var root = JsonNode.Parse(File.ReadAllText(appSettingsPath))?.AsObject()
        ?? throw new InvalidOperationException("App settings file is empty or invalid JSON.");

    var kestrel = EnsureObject(root, "Kestrel");
    var endpoints = EnsureObject(kestrel, "Endpoints");

    if (!endpoints.ContainsKey("Http"))
    {
        Log.Information("HTTP endpoint was not found. Adding default HTTP endpoint.");
        endpoints["Http"] = new JsonObject
        {
            ["Url"] = "http://0.0.0.0:5770"
        };
    }

    switch (options.Mode)
    {
        case TransportMode.Http:
            endpoints.Remove("Https");
            break;

        case TransportMode.ExistingPfx:
            ConfigureExistingPfx(endpoints, options);
            break;

        case TransportMode.SelfSigned:
            ConfigureSelfSigned(endpoints, options);
            break;
    }

    var jsonOptions = new JsonSerializerOptions
    {
        WriteIndented = true
    };

    Log.Information("Writing updated appsettings JSON to {AppSettingsPath}.", appSettingsPath);
    File.WriteAllText(appSettingsPath, root.ToJsonString(jsonOptions));
}

static JsonObject EnsureObject(JsonObject parent, string name)
{
    if (parent[name] is JsonObject existing)
    {
        return existing;
    }

    var created = new JsonObject();
    parent[name] = created;
    return created;
}

static void ConfigureExistingPfx(JsonObject endpoints, InstallerOptions options)
{
    Log.Information("Configuring HTTPS endpoint using existing PFX certificate.");

    if (string.IsNullOrWhiteSpace(options.ExistingPfxPath))
    {
        throw new InvalidOperationException("Existing PFX path is required.");
    }

    if (string.IsNullOrWhiteSpace(options.ExistingPfxPassword))
    {
        throw new InvalidOperationException("Existing PFX password is required.");
    }

    if (!File.Exists(options.ExistingPfxPath))
    {
        Log.Error("Existing PFX file was not found at {ExistingPfxPath}.", options.ExistingPfxPath);
        throw new FileNotFoundException("Existing PFX file was not found.", options.ExistingPfxPath);
    }

    Log.Information("Using existing PFX file {ExistingPfxPath}.", options.ExistingPfxPath);
    SetHttpsEndpoint(endpoints, options.ExistingPfxPath, options.ExistingPfxPassword);
}

static void ConfigureSelfSigned(JsonObject endpoints, InstallerOptions options)
{
    Log.Information("Configuring HTTPS endpoint using a self-signed certificate.");

    if (string.IsNullOrWhiteSpace(options.DnsNames))
    {
        throw new InvalidOperationException("DNS names or IP addresses are required for a self-signed certificate.");
    }

    if (string.IsNullOrWhiteSpace(options.GeneratedPfxPath))
    {
        throw new InvalidOperationException("Generated PFX path is required.");
    }

    if (string.IsNullOrWhiteSpace(options.GeneratedPfxPassword))
    {
        throw new InvalidOperationException("Generated PFX password is required.");
    }

    var names = options.DnsNames
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Where(name => !string.IsNullOrWhiteSpace(name))
        .ToArray();

    if (names.Length == 0)
    {
        throw new InvalidOperationException("DNS names or IP addresses are required for a self-signed certificate.");
    }

    Log.Information("Self-signed certificate subject names: {Names}.", string.Join(", ", names));

    var certificateDirectory = Path.GetDirectoryName(options.GeneratedPfxPath);
    if (!string.IsNullOrWhiteSpace(certificateDirectory))
    {
        Log.Information("Ensuring certificate directory exists at {CertificateDirectory}.", certificateDirectory);
        Directory.CreateDirectory(certificateDirectory);
    }

    Log.Information("Generating self-signed certificate to {GeneratedPfxPath}.", options.GeneratedPfxPath);
    var pfxBytes = CreateSelfSignedCertificate(names, options.GeneratedPfxPassword);
    File.WriteAllBytes(options.GeneratedPfxPath, pfxBytes);

    SetHttpsEndpoint(endpoints, options.GeneratedPfxPath, options.GeneratedPfxPassword);
}

static byte[] CreateSelfSignedCertificate(string[] names, string password)
{
    Log.Information("Creating self-signed certificate for primary subject {PrimaryName}.", names[0]);

    using var rsa = RSA.Create(2048);
    var subject = new X500DistinguishedName($"CN={names[0]}");
    var request = new CertificateRequest(subject, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

    request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
    request.CertificateExtensions.Add(new X509KeyUsageExtension(
        X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
        critical: true));
    request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
        new OidCollection
        {
            new Oid("1.3.6.1.5.5.7.3.1")
        },
        critical: false));

    var sanBuilder = new SubjectAlternativeNameBuilder();
    foreach (var name in names)
    {
        if (IPAddress.TryParse(name, out var address))
        {
            sanBuilder.AddIpAddress(address);
        }
        else
        {
            sanBuilder.AddDnsName(name);
        }
    }

    request.CertificateExtensions.Add(sanBuilder.Build());

    using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddYears(3));
    return certificate.Export(X509ContentType.Pfx, password);
}

static void SetHttpsEndpoint(JsonObject endpoints, string path, string password)
{
    Log.Information("Setting HTTPS endpoint with certificate path {CertificatePath}.", path);

    endpoints["Https"] = new JsonObject
    {
        ["Url"] = "https://0.0.0.0:5771",
        ["Certificate"] = new JsonObject
        {
            ["Path"] = path,
            ["Password"] = password
        }
    };
}

static Serilog.ILogger CreateLogger(string logPath)
{
    return new LoggerConfiguration()
        .MinimumLevel.Debug()
        .Enrich.FromLogContext()
        .WriteTo.File(
            path: logPath,
            shared: true,
            flushToDiskInterval: TimeSpan.FromSeconds(1),
            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
        .CreateLogger();
}

static string GetLogPath()
{
    var logDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "FSCStorage");
    Directory.CreateDirectory(logDirectory);
    return Path.Combine(logDirectory, "installer-tool.log");
}

static void LogOptions(InstallerOptions options)
{
    Log.Information(
        "Parsed installer options. Mode: {Mode}, InstallDir: {InstallDir}, AppSettingsPath: {AppSettingsPath}, ExistingPfxPath: {ExistingPfxPath}, GeneratedPfxPath: {GeneratedPfxPath}, DnsNames: {DnsNames}",
        options.Mode,
        options.InstallDir,
        options.AppSettingsPath,
        options.ExistingPfxPath,
        options.GeneratedPfxPath,
        options.DnsNames);
}

internal enum TransportMode
{
    Http,
    ExistingPfx,
    SelfSigned
}

internal sealed class InstallerOptions
{
    public string? InstallDir { get; set; }
    public string? AppSettingsPath { get; set; }
    public TransportMode Mode { get; set; } = TransportMode.Http;
    public string? ExistingPfxPath { get; set; }
    public string? ExistingPfxPassword { get; set; }
    public string? GeneratedPfxPath { get; set; }
    public string? GeneratedPfxPassword { get; set; }
    public string? DnsNames { get; set; }

    public static InstallerOptions Parse(string[] args)
    {
        var options = new InstallerOptions();

        for (var i = 0; i < args.Length; i++)
        {
            var name = args[i];
            var value = i + 1 < args.Length ? args[++i] : throw new ArgumentException($"Missing value for argument '{name}'.");

            switch (name.ToLowerInvariant())
            {
                case "-i":
                case "--install-dir":
                    options.InstallDir = value;
                    break;

                case "-a":
                case "--appsettings":
                    options.AppSettingsPath = value;
                    break;

                case "-m":
                case "--mode":
                    options.Mode = value.ToUpperInvariant() switch
                    {
                        "HTTP" => TransportMode.Http,
                        "EXISTING_PFX" => TransportMode.ExistingPfx,
                        "SELF_SIGNED" => TransportMode.SelfSigned,
                        _ => throw new ArgumentException($"Unsupported transport mode '{value}'.")
                    };
                    break;

                case "-x":
                case "--existing-pfx":
                    options.ExistingPfxPath = value;
                    break;

                case "-xp":
                case "--existing-pfx-password":
                    options.ExistingPfxPassword = value;
                    break;

                case "-g":
                case "--generated-pfx":
                    options.GeneratedPfxPath = value;
                    break;

                case "-gp":
                case "--generated-pfx-password":
                    options.GeneratedPfxPassword = value;
                    break;

                case "-n":
                case "--names":
                    options.DnsNames = value;
                    break;

                default:
                    throw new ArgumentException($"Unsupported argument '{name}'.");
            }
        }

        return options;
    }
}
