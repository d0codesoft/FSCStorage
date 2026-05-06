using Microsoft.AspNetCore.DataProtection;

namespace scp.filestorage.Services.Auth
{
    public static class AuthenticationSecretProtectionExtensions
    {
        public static WebApplicationBuilder RegisterAuthenticationSecretProtection(this WebApplicationBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            var authSecretProtectionSection =
                builder.Configuration.GetSection("Authentication:SecretProtection");

            builder.Services.Configure<AuthenticationSecretProtectorOptions>(
                authSecretProtectionSection);

            var secretProtectionOptions =
                authSecretProtectionSection.Get<AuthenticationSecretProtectorOptions>()
                ?? new AuthenticationSecretProtectorOptions();

            var dataProtectionBuilder = builder.Services
                .AddDataProtection()
                .SetApplicationName(secretProtectionOptions.ApplicationName);

            if (OperatingSystem.IsLinux())
            {
                var keyDirectory = secretProtectionOptions.LinuxKeyDirectory;

                if (string.IsNullOrWhiteSpace(keyDirectory))
                {
                    keyDirectory = Path.Combine(
                        AppContext.BaseDirectory,
                        "App_Data",
                        "DataProtectionKeys");
                }

                Directory.CreateDirectory(keyDirectory);

                dataProtectionBuilder.PersistKeysToFileSystem(new DirectoryInfo(keyDirectory));
            }

            builder.Services.AddScoped<IAuthenticationSecretProtector, AuthenticationSecretProtector>();

            return builder;
        }
    }
}
