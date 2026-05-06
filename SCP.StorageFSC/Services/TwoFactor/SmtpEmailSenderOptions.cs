namespace scp.filestorage.Services.TwoFactor
{
    /// <summary>
    /// Defines SMTP settings used by the email sender.
    /// </summary>
    public sealed class SmtpEmailSenderOptions
    {
        /// <summary>
        /// SMTP server host name.
        /// </summary>
        public string Host { get; set; } = string.Empty;

        /// <summary>
        /// SMTP server port.
        /// </summary>
        public int Port { get; set; } = 587;

        /// <summary>
        /// Indicates whether SSL/TLS should be used.
        /// </summary>
        public bool EnableSsl { get; set; } = true;

        /// <summary>
        /// SMTP user name.
        /// </summary>
        public string? UserName { get; set; }

        /// <summary>
        /// SMTP password.
        /// </summary>
        public string? Password { get; set; }

        /// <summary>
        /// Email address used as the sender address.
        /// </summary>
        public string FromAddress { get; set; } = "no-reply@localhost";

        /// <summary>
        /// Display name used as the sender name.
        /// </summary>
        public string FromName { get; set; } = "FSCStorage";
    }
}
