namespace scp.filestorage.Services.TwoFactor
{
    /// <summary>
    /// Defines settings used for TOTP authentication.
    /// </summary>
    public sealed class TotpOptions
    {
        /// <summary>
        /// Issuer name displayed in authenticator applications.
        /// </summary>
        public string Issuer { get; set; } = "FSCStorage";

        /// <summary>
        /// TOTP step duration in seconds.
        /// Standard value is 30 seconds.
        /// </summary>
        public int StepSeconds { get; set; } = 30;

        /// <summary>
        /// Number of TOTP digits.
        /// Standard value is 6 digits.
        /// </summary>
        public int CodeDigits { get; set; } = 6;

        /// <summary>
        /// Number of allowed time steps before and after the current step.
        /// Value 1 means previous, current, and next time window are accepted.
        /// </summary>
        public int VerificationWindowSteps { get; set; } = 1;

        /// <summary>
        /// Secret size in bytes.
        /// 20 bytes is a common value for TOTP secrets.
        /// </summary>
        public int SecretSizeBytes { get; set; } = 20;
    }
}
