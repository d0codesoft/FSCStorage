using scp.filestorage.Data.Models;
using scp.filestorage.Data.Repositories;
using scp.filestorage.Security;
using scp.filestorage.Services.Auth;
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

            var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
            var roleRepository = scope.ServiceProvider.GetRequiredService<IRoleRepository>();
            var userRoleRepository = scope.ServiceProvider.GetRequiredService<IUserRoleRepository>();
            var tenantRepository = scope.ServiceProvider.GetRequiredService<ITenantRepository>();
            var apiTokenRepository = scope.ServiceProvider.GetRequiredService<IApiTokenRepository>();
            var passwordHashService = scope.ServiceProvider.GetRequiredService<IPasswordHashService>();

            var baseDirectory = AppContext.BaseDirectory;
            var configPath = Path.Combine(baseDirectory, AdminConfigFileName);

            AdminBootstrapConfig config;
            var configChanged = false;

            if (File.Exists(configPath))
            {
                logger.LogInformation("Admin config file found: {ConfigPath}", configPath);

                var json = await File.ReadAllTextAsync(configPath, cancellationToken);
                config = JsonSerializer.Deserialize<AdminBootstrapConfig>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? throw new InvalidOperationException("Failed to parse admin.conf.");

                if (string.IsNullOrWhiteSpace(config.Name))
                {
                    config.Name = "Administrator";
                    configChanged = true;
                }

                if (string.IsNullOrWhiteSpace(config.Key))
                {
                    config.Key = TokenHashHelper.GenerateToken();
                    configChanged = true;
                }

                if (string.IsNullOrWhiteSpace(config.Password))
                {
                    config.Password = StrongTokenGenerator.GenerateStrong(16, true, true, true, true, "*&%$#@");
                    configChanged = true;
                }
            }
            else
            {
                logger.LogWarning("Admin config file not found. Generating new admin credentials.");

                config = new AdminBootstrapConfig
                {
                    Name = "Administrator",
                    Key = TokenHashHelper.GenerateToken(),
                    Password = StrongTokenGenerator.GenerateStrong(16, true, true, true, true,"*&%$#@")
                };

                configChanged = true;
            }

            if (configChanged)
            {
                var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                await File.WriteAllTextAsync(configPath, json, cancellationToken);

                logger.LogWarning("Admin config file saved: {ConfigPath}", configPath);
            }

            var adminUser = await userRepository.GetByNormalizedNameAsync(
                Normalize(config.Name),
                cancellationToken);

            if (adminUser is null)
            {
                adminUser = new User
                {
                    Name = config.Name,
                    NormalizedName = Normalize(config.Name),
                    Email = $"{config.Name}@example.com",
                    NormalizedEmail = $"{Normalize(config.Name)}@EXAMPLE.COM",
                    PasswordHash = string.Empty,
                    IsActive = true,
                    TwoFactorEnabled = false,
                    TwoFactorRequiredForEveryLogin = false,
                    PreferredTwoFactorMethod = TwoFactorMethodType.None,
                    CreatedUtc = DateTime.UtcNow
                };

                adminUser.PasswordHash = passwordHashService.HashPassword(
                    adminUser,
                    config.Password);

                var userCreated = await userRepository.InsertAsync(adminUser, cancellationToken);
                if (!userCreated)
                {
                    logger.LogError("Failed to insert administrative user into database.");
                    throw new InvalidOperationException("Failed to create administrative user.");
                }

                logger.LogInformation(
                    "Administrative user created. UserId={UserId}, UserName={UserName}",
                    adminUser.Id,
                    adminUser.Name);
            }
            else
            {
                logger.LogInformation(
                    "Administrative user already exists. UserId={UserId}, UserName={UserName}",
                    adminUser.Id,
                    adminUser.Name);
            }

            var adminRole = await roleRepository.GetByNormalizedNameAsync(
                SystemRoles.AdministratorNormalized,
                cancellationToken);

            if (adminRole is null)
            {
                adminRole = new Role
                {
                    Id = SystemRoles.AdministratorId,
                    Name = SystemRoles.Administrator,
                    NormalizedName = SystemRoles.AdministratorNormalized,
                    Description = "Full system administrator role.",
                    IsSystem = true,
                    CreatedUtc = DateTime.UtcNow
                };

                var roleCreated = await roleRepository.InsertAsync(adminRole, cancellationToken);
                if (!roleCreated)
                {
                    logger.LogError("Failed to insert administrative role into database.");
                    throw new InvalidOperationException("Failed to create administrative role.");
                }

                logger.LogInformation(
                    "Administrative role created. RoleId={RoleId}, RoleName={RoleName}",
                    adminRole.Id,
                    adminRole.Name);
            }

            var adminUserRole = await userRoleRepository.GetByUserIdAndRoleIdAsync(
                adminUser.Id,
                adminRole.Id,
                cancellationToken);

            if (adminUserRole is null)
            {
                adminUserRole = new UserRole
                {
                    UserId = adminUser.Id,
                    RoleId = adminRole.Id,
                    CreatedUtc = DateTime.UtcNow
                };

                var userRoleCreated = await userRoleRepository.InsertAsync(adminUserRole, cancellationToken);
                if (!userRoleCreated)
                {
                    logger.LogError("Failed to assign administrative role to bootstrap user.");
                    throw new InvalidOperationException("Failed to assign administrative role.");
                }

                logger.LogInformation(
                    "Administrative role assigned to user. UserId={UserId}, RoleId={RoleId}",
                    adminUser.Id,
                    adminRole.Id);
            }

            var adminTenant = await tenantRepository.GetByNameAsync(config.Name, cancellationToken);

            if (adminTenant is null)
            {
                adminTenant = new Tenant
                {
                    UserId = adminUser.Id,
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
            else if (adminTenant.UserId != adminUser.Id)
            {
                adminTenant.UserId = adminUser.Id;
                adminTenant.MarkUpdated();

                var tenantUpdated = await tenantRepository.UpdateAsync(adminTenant, cancellationToken);
                if (!tenantUpdated)
                {
                    logger.LogError("Failed to update administrative tenant owner user.");
                    throw new InvalidOperationException("Failed to update administrative tenant.");
                }

                logger.LogInformation(
                    "Administrative tenant owner updated. TenantId={TenantId}, UserId={UserId}",
                    adminTenant.Id,
                    adminUser.Id);
            }
            else
            {
                logger.LogInformation(
                    "Administrative tenant already exists. TenantId={TenantId}, TenantGuid={TenantGuid}",
                    adminTenant.Id,
                    adminTenant.ExternalTenantId);
            }

            if (await apiTokenRepository.HasAnyAdminTokenAsync(cancellationToken))
            {
                logger.LogInformation("Administrative API token already exists. Token bootstrap skipped.");
                return app;
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
                UserId = adminUser.Id,
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

        private static string Normalize(string value)
        {
            return value.Trim().ToUpperInvariant();
        }
    }
}
