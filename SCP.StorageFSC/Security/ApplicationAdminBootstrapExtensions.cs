using SCP.StorageFSC.Data.Models;
using SCP.StorageFSC.Data.Repositories;
using System.Text.Json;

namespace SCP.StorageFSC.Security
{
    public static class ApplicationAdminBootstrapExtensions
    {
        private const string AdminConfigFileName = "admin.conf";

        public static async Task<WebApplication> InitializeAdminTokenAsync(
            this WebApplication app,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(app);

            using var scope = app.Services.CreateScope();

            var logger = scope.ServiceProvider
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("AdminBootstrap");

            var tenantRepository = scope.ServiceProvider.GetRequiredService<ITenantRepository>();
            var apiTokenRepository = scope.ServiceProvider.GetRequiredService<IApiTokenRepository>();

            if (await apiTokenRepository.HasAnyAdminTokenAsync(cancellationToken))
            {
                logger.LogInformation("Administrative API token already exists. Bootstrap skipped.");
                return app;
            }

            var baseDirectory = AppContext.BaseDirectory;
            var configPath = Path.Combine(baseDirectory, AdminConfigFileName);

            AdminBootstrapConfig config;

            if (File.Exists(configPath))
            {
                logger.LogInformation("Admin config file found: {ConfigPath}", configPath);

                var json = await File.ReadAllTextAsync(configPath, cancellationToken);
                config = JsonSerializer.Deserialize<AdminBootstrapConfig>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? throw new InvalidOperationException("Failed to parse admin.conf.");

                if (string.IsNullOrWhiteSpace(config.Name))
                    config.Name = "Administrator";

                if (string.IsNullOrWhiteSpace(config.Key))
                    throw new InvalidOperationException("admin.conf exists but key is empty.");
            }
            else
            {
                logger.LogWarning("Admin config file not found. Generating new admin token.");

                config = new AdminBootstrapConfig
                {
                    Name = "Administrator",
                    Key = TokenHashHelper.GenerateToken()
                };

                var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                await File.WriteAllTextAsync(configPath, json, cancellationToken);

                logger.LogWarning("Admin config file created: {ConfigPath}", configPath);
            }

            var adminTenant = await tenantRepository.GetByNameAsync(config.Name, cancellationToken);

            if (adminTenant is null)
            {
                adminTenant = new Tenant
                {
                    ExternalTenantId = Guid.NewGuid(),
                    Name = config.Name,
                    IsActive = true,
                    CreatedUtc = DateTime.UtcNow,
                };

                var result = await tenantRepository.InsertAsync(adminTenant, cancellationToken);
                if (!result)
                {
                    logger.LogError("Failed to insert administrative tenant into database.");
                    throw new InvalidOperationException("Failed to create administrative tenant.");
                }

                logger.LogInformation(
                    "Administrative tenant created. TenantId={TenantId}, ExternalTenantId={ExternalTenantId}",
                    adminTenant.Id,
                    adminTenant.ExternalTenantId);
            }
            else
            {
                logger.LogInformation(
                    "Administrative tenant already exists. TenantId={TenantId}, TenantGuid={TenantGuid}",
                    adminTenant.Id,
                    adminTenant.ExternalTenantId);
            }

            var tokenHash = TokenHashHelper.ComputeSha256(config.Key);

            var existingToken = await apiTokenRepository.GetByHashAsync(tokenHash, cancellationToken);
            if (existingToken is not null)
            {
                logger.LogInformation("Administrative token already exists in database.");
                return app;
            }

            var token = new ApiToken
            {
                TenantId = adminTenant.Id,
                Name = "Bootstrap Admin Token",
                TokenHash = tokenHash,
                TokenPrefix = TokenHashHelper.GetPrefix(config.Key),
                IsActive = true,
                CanRead = true,
                CanWrite = true,
                CanDelete = true,
                IsAdmin = true,
                CreatedUtc = DateTime.UtcNow
            };

            _ = await apiTokenRepository.InsertAsync(token, cancellationToken);

            logger.LogInformation(
                "Administrative API token created. TokenId={TokenId}, TenantId={TenantId}, Prefix={TokenPrefix}",
                token.Id,
                adminTenant.Id,
                token.TokenPrefix);

            return app;
        }
    }
}
