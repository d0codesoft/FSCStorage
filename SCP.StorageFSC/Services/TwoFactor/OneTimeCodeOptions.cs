namespace scp.filestorage.Services.TwoFactor
{
    /// <summary>
    /// Defines settings used for sending one-time authentication codes.
    /// </summary>
    public sealed class OneTimeCodeOptions
    {
        /// <summary>
        /// Display name used as the sender name in email messages.
        /// </summary>
        public string EmailSenderName { get; set; } = "FSCStorage";

        /// <summary>
        /// Email address used as the sender address.
        /// </summary>
        public string EmailSenderAddress { get; set; } = "no-reply@localhost";

        /// <summary>
        /// Subject used for one-time authentication code emails.
        /// </summary>
        public string EmailSubject { get; set; } = "Your verification code";

        /// <summary>
        /// Number of minutes while the one-time code is expected to be valid.
        /// </summary>
        public int CodeLifetimeMinutes { get; set; } = 5;

        /// <summary>
        /// Indicates whether SMS sending is enabled.
        /// </summary>
        public bool SmsEnabled { get; set; }
    }
}
