namespace scp.filestorage.Services.Auth
{
    /// <summary>
    /// Defines settings used for authentication secret protection.
    /// </summary>
    public sealed class AuthenticationSecretProtectorOptions
    {
        /// <summary>
        /// Directory where DataProtection keys are stored on Linux.
        /// </summary>
        public string? LinuxKeyDirectory { get; set; }

        /// <summary>
        /// Application name used to isolate DataProtection keys.
        /// </summary>
        public string ApplicationName { get; set; } = "FSCStorage";

        /// <summary>
        /// Purpose string used to isolate authentication secret encryption.
        /// </summary>
        public string Purpose { get; set; } = "FSCStorage.AuthenticationSecrets.v1";
    }
}
