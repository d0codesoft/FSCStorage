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

builder.Services.Configure<FileStorageOptions>(
    builder.Configuration.GetSection("Paths"));

builder.InitializeLogging();

var basePath = builder.Configuration["Paths:BasePath"];
if (basePath == null)
    basePath = Path.Combine(Directory.GetCurrentDirectory(), "files");

var connectionString = $"Data Source={Path.Combine(basePath, "storage.db")}";

builder.Services.AddSingleton<ICurrentTenantAccessor, CurrentTenantAccessor>();
builder.Services.AddHttpContextAccessor();

builder.Services.AddSingleton<IDbConnectionFactory>(
    _ => new SqliteConnectionFactory(connectionString));

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

builder.Services.Configure<FileStorageMultipartOptions>(
    builder.Configuration.GetSection("FileStorageMultipart"));

builder.Services.AddScoped<IFileStorageMultipartService, FileStorageMultipartService>();

builder.Services.AddControllers();

builder.Services.AddOpenApi();

builder.Services.AddApiTokenAuthentication();

var app = builder.Build();

await app.InitializeDatabaseAsync();
await app.InitializeAdminTokenAsync();

app.UseApplicationRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseApiTokenAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
