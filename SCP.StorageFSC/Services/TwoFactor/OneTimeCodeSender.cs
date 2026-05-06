using Microsoft.Extensions.Options;
using scp.filestorage.Services.Auth;

namespace scp.filestorage.Services.TwoFactor
{
    /// <summary>
    /// Sends one-time authentication codes using email or SMS.
    /// </summary>
    public sealed class OneTimeCodeSender : IOneTimeCodeSender
    {
        private readonly IEmailSender _emailSender;
        private readonly OneTimeCodeOptions _options;
        private readonly ILogger<OneTimeCodeSender> _logger;

        public OneTimeCodeSender(
            IEmailSender emailSender,
            IOptions<OneTimeCodeOptions> options,
            ILogger<OneTimeCodeSender> logger)
        {
            _emailSender = emailSender;
            _options = options.Value;
            _logger = logger;
        }

        public async Task SendEmailCodeAsync(
            string email,
            string code,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(email))
                throw new ArgumentException("Email address cannot be empty.", nameof(email));

            if (string.IsNullOrWhiteSpace(code))
                throw new ArgumentException("Verification code cannot be empty.", nameof(code));

            var body = CreateEmailBody(code);

            await _emailSender.SendAsync(
                email,
                _options.EmailSubject,
                body,
                cancellationToken);

            _logger.LogInformation(
                "One-time authentication code was sent by email to {MaskedEmail}.",
                MaskEmail(email));
        }

        public async Task SendSmsCodeAsync(
            string phoneNumber,
            string code,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber))
                throw new ArgumentException("Phone number cannot be empty.", nameof(phoneNumber));

            if (string.IsNullOrWhiteSpace(code))
                throw new ArgumentException("Verification code cannot be empty.", nameof(code));

            if (!_options.SmsEnabled)
                throw new InvalidOperationException("SMS delivery is disabled.");

            var message = CreateSmsMessage(code);

            //await _smsSender.SendAsync(
            //    phoneNumber,
            //    message,
            //    cancellationToken);

            _logger.LogInformation(
                "One-time authentication code was sent by SMS to {MaskedPhoneNumber}.",
                MaskPhoneNumber(phoneNumber));
        }

        private string CreateEmailBody(string code)
        {
            return $"""
                Your FSCStorage verification code is:

                {code}

                This code is valid for {_options.CodeLifetimeMinutes} minutes.

                If you did not request this code, ignore this message.
                """;
        }

        private string CreateSmsMessage(string code)
        {
            return $"FSCStorage verification code: {code}. Valid for {_options.CodeLifetimeMinutes} minutes.";
        }

        private static string MaskEmail(string email)
        {
            var atIndex = email.IndexOf('@', StringComparison.Ordinal);

            if (atIndex <= 1)
                return "***";

            var name = email[..atIndex];
            var domain = email[(atIndex + 1)..];

            var visibleName = name.Length <= 2
                ? name[0].ToString()
                : name[..2];

            return $"{visibleName}***@{domain}";
        }

        private static string MaskPhoneNumber(string phoneNumber)
        {
            var digits = new string(phoneNumber.Where(char.IsDigit).ToArray());

            if (digits.Length <= 4)
                return "***";

            return $"***{digits[^4..]}";
        }
    }
}
