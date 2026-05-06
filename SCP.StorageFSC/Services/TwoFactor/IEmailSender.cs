namespace scp.filestorage.Services.TwoFactor
{
    /// <summary>
    /// Sends email messages.
    /// </summary>
    public interface IEmailSender
    {
        /// <summary>
        /// Sends an email message.
        /// </summary>
        Task SendAsync(
            string to,
            string subject,
            string body,
            CancellationToken cancellationToken = default);
    }
}
