using scp.filestorage.Data.Dto;
using scp.filestorage.Data.Repositories;
using scp.filestorage.InterfacesService;
using scp.filestorage.Services;
using SCP.StorageFSC;
using SCP.StorageFSC.Data;
using SCP.StorageFSC.Data.Repositories;
using SCP.StorageFSC.InterfacesService;
using SCP.StorageFSC.Security;
using SCP.StorageFSC.SecurityPermission;
using SCP.StorageFSC.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseWindowsService();

builder.Configuration
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile(
        OperatingSystem.IsWindows()
            ? "appsettings.Windows.json"
            : "appsettings.Linux.json",
        optional: true,
        reloadOnChange: true)
    .AddEnvironmentVariables();

builder.Services.RegisterDatabase();

builder.Services.Configure<FileStorageOptions>(
    builder.Configuration.GetSection("Paths"));

var applicationPaths = ApplicationPaths.FromConfiguration(builder.Configuration);
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

builder.Services.Configure<FileStorageMultipartOptions>(
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

builder.Services.AddOpenApi();

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
