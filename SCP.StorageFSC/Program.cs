using Microsoft.Extensions.FileProviders;
using scp.filestorage;
using scp.filestorage.Data.Dto;
using scp.filestorage.Data.Repositories;
using scp.filestorage.InterfacesService;
using scp.filestorage.Services;
using scp.filestorage.Services.Auth;
using scp.filestorage.Services.TwoFactor;
using SCP.StorageFSC;
using SCP.StorageFSC.Data;
using SCP.StorageFSC.Data.Repositories;
using SCP.StorageFSC.InterfacesService;
using SCP.StorageFSC.Security;
using SCP.StorageFSC.SecurityPermission;
using SCP.StorageFSC.Services;

// Get parameters from command line arguments
program_utils.ParseArguments(args);

var configFilePath = program_utils.GetValueArg("config") ?? Environment.GetEnvironmentVariable("FSCStore_Config");
var baseDirPath = program_utils.GetValueArg("base_dir") ?? Environment.GetEnvironmentVariable("FSCStore_BaseDir");

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
});

builder.Configuration.SetBasePath(builder.Environment.ContentRootPath);

// Configure hosting and services based on the operating system
if (OperatingSystem.IsWindows())
{
    builder.Host.UseWindowsService();
    builder.Services.AddWindowsService();
}
else if (OperatingSystem.IsLinux())
{
    builder.Host.UseSystemd();
}

if (string.IsNullOrEmpty(configFilePath))
{
    builder.Configuration
        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
        .AddEnvironmentVariables();
}
else
{
    var fullConfigFilePath = Path.GetFullPath(configFilePath);
    var configDirectory = Path.GetDirectoryName(fullConfigFilePath);
    var configFileName = Path.GetFileName(fullConfigFilePath);

    if (string.IsNullOrWhiteSpace(configDirectory) || string.IsNullOrWhiteSpace(configFileName))
    {
        throw new ArgumentException("The --config parameter must contain a valid file path.");
    }

    builder.Configuration.AddJsonFile(
            provider: new PhysicalFileProvider(configDirectory),
            path: configFileName,
            optional: false,
            reloadOnChange: true)
        .AddEnvironmentVariables();
}

builder.Services.RegisterDatabase();

// Initialize directory paths
var applicationPaths = ApplicationPaths.FromConfiguration(builder.Configuration, baseDirPath);
builder.Services.AddSingleton(applicationPaths);

builder.InitializeDataFolder(applicationPaths);
builder.InitializeLogging(applicationPaths);

var connectionString = $"Data Source={Path.Combine(applicationPaths.BasePath, "storage.db")}";

builder.Services.AddSingleton<ICurrentTenantAccessor, CurrentTenantAccessor>();
builder.Services.AddHttpContextAccessor();

builder.Services.AddSingleton<IDbConnectionFactory>(
    serviceProvider => new SqliteConnectionFactory(
        connectionString,
        serviceProvider.GetRequiredService<ILogger<SqliteConnectionFactory>>()));

builder.Services.AddSingleton<IDbInitializer, DbInitializer>();

builder.Services.AddScoped<IApiTokenService, ApiTokenService>();
builder.Services.AddScoped<IApiAuthenticationAuditService, ApiAuthenticationAuditService>();
builder.Services.AddScoped<ITenantAuthorizationService, TenantAuthorizationService>();

builder.Services.AddScoped<ITenantRepository, TenantRepository>();
builder.Services.AddScoped<IApiTokenRepository, ApiTokenRepository>();
builder.Services.AddScoped<IApiTokenConnectionLogRepository, ApiTokenConnectionLogRepository>();
builder.Services.AddScoped<IStoredFileRepository, StoredFileRepository>();
builder.Services.AddScoped<ITenantFileRepository, TenantFileRepository>();
builder.Services.AddScoped<ITenantStorageService, TenantStorageService>();
builder.Services.AddScoped<IFileStorageService, FileStorageService>();
builder.Services.AddScoped<IMultipartUploadSessionRepository, MultipartUploadSessionRepository>();
builder.Services.AddScoped<IMultipartUploadPartRepository, MultipartUploadPartRepository>();
builder.Services.AddScoped<IBackgroundTaskRepository, BackgroundTaskRepository>();
builder.Services.AddScoped<IStorageStatisticsRepository, StorageStatisticsRepository>();
builder.Services.AddScoped<IDeletedTenantRepository, DeletedTenantRepository>();
builder.Services.AddScoped<IDeletedTenantCleanupService, DeletedTenantCleanupService>();

builder.Services.AddUserManagementRepositories();

builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();

builder.Services.AddScoped<IPasswordHashService, PasswordHashService>();
builder.Services.AddScoped<IAuthenticationHashService, AuthenticationHashService>();

builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();

builder.Services.Configure<TotpOptions>(
    builder.Configuration.GetSection("Authentication:Totp"));

builder.RegisterAuthenticationSecretProtection(applicationPaths);
builder.Services.AddScoped<ITotpService, TotpService>();
builder.Services.AddScoped<IOneTimeCodeSender, OneTimeCodeSender>();
builder.Services.AddScoped<IQrCodeService, QrCodeService>();


builder.Services.Configure<MultipartSettingOptions>(
    builder.Configuration.GetSection("FileStorageMultipart"));

builder.Services.Configure<FileStorageCleanupOptions>(
    builder.Configuration.GetSection("FileStorageCleanup"));

builder.Services.AddSingleton<IFileStorageBackgroundTaskQueue, FileStorageBackgroundTaskQueue>();
builder.Services.AddScoped<IFileStorageConsistencyService, FileStorageConsistencyService>();
builder.Services.AddScoped<IMultipartUploadBackgroundTaskProcessor, MultipartUploadBackgroundTaskProcessor>();
builder.Services.AddScoped<IFileStorageMultipartService, FileStorageMultipartService>();
builder.Services.AddHostedService<FileStorageBackgroundService>();
builder.Services.AddHostedService<FileStorageCleanupBackgroundService>();

builder.Services.AddControllers();

builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer<OpenApiSecurityDocumentTransformer>();
});

builder.Services.AddApiTokenAuthentication();

var app = builder.Build();

await app.InitializeDatabaseAsync();
await app.InitializeAdminTokenAsync();

app.UseApplicationRequestLogging();
app.UseApplicationExceptionHandling();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

app.UseAuthentication();
app.UseApiTokenAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapFallbackToFile("index.html");

app.Run();