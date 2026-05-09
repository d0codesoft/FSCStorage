using Microsoft.AspNetCore.DataProtection;
using SCP.StorageFSC;

namespace scp.filestorage.Services.Auth
{
    public static class AuthenticationSecretProtectionExtensions
    {
        public static WebApplicationBuilder RegisterAuthenticationSecretProtection(this WebApplicationBuilder builder, ApplicationPaths applicationPaths)
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
                    keyDirectory = Path.Combine(applicationPaths.BasePath, "DataProtectionKeys");
                }

                if (Directory.Exists(keyDirectory))
                {
                    // Ensure the directory is writable
                    try
                    {
                        using var testFile = File.Create(Path.Combine(keyDirectory, "test.tmp"));
                        testFile.Close();
                        File.Delete(testFile.Name);
                    }
                    catch
                    {
                        throw new InvalidOperationException($"The directory '{keyDirectory}' is not writable. Please specify a writable directory for data protection keys in the configuration.");
                    }
                }
                else
                {
                    try
                    {
                        Directory.CreateDirectory(keyDirectory);
                    }
                    catch
                    {
                        throw new InvalidOperationException("Failed to create directory for data protection keys. Please specify a valid directory in the configuration.");
                    }
                }

                dataProtectionBuilder.PersistKeysToFileSystem(new DirectoryInfo(keyDirectory));
            }

            builder.Services.AddScoped<IAuthenticationSecretProtector, AuthenticationSecretProtector>();

            return builder;
        }
    }
}
