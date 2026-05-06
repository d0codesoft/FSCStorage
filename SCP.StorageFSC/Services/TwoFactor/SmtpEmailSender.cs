using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Mail;

namespace scp.filestorage.Services.TwoFactor
{
    /// <summary>
    /// Sends email messages using SMTP.
    /// </summary>
    public sealed class SmtpEmailSender : IEmailSender
    {
        private readonly SmtpEmailSenderOptions _options;

        public SmtpEmailSender(IOptions<SmtpEmailSenderOptions> options)
        {
            _options = options.Value;
        }

        public async Task SendAsync(
            string to,
            string subject,
            string body,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(to))
                throw new ArgumentException("Recipient email address cannot be empty.", nameof(to));

            if (string.IsNullOrWhiteSpace(subject))
                throw new ArgumentException("Email subject cannot be empty.", nameof(subject));

            if (string.IsNullOrWhiteSpace(_options.Host))
                throw new InvalidOperationException("SMTP host is not configured.");

            using var message = new MailMessage
            {
                From = new MailAddress(_options.FromAddress, _options.FromName),
                Subject = subject,
                Body = body,
                IsBodyHtml = false
            };

            message.To.Add(to);

            using var client = new SmtpClient(_options.Host, _options.Port)
            {
                EnableSsl = _options.EnableSsl
            };

            if (!string.IsNullOrWhiteSpace(_options.UserName))
            {
                client.Credentials = new NetworkCredential(
                    _options.UserName,
                    _options.Password);
            }

            await client.SendMailAsync(message, cancellationToken);
        }
    }
}
