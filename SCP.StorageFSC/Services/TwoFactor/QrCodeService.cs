using QRCoder;
using scp.filestorage.Services.Auth;

namespace scp.filestorage.Services.TwoFactor
{
    /// <summary>
    /// Generates QR codes for authenticator application setup.
    /// </summary>
    public sealed class QrCodeService : IQrCodeService
    {
        public string GeneratePngBase64(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                throw new ArgumentException("QR code text cannot be empty.", nameof(text));

            using var generator = new QRCodeGenerator();
            using var data = generator.CreateQrCode(text, QRCodeGenerator.ECCLevel.Q);

            var qrCode = new PngByteQRCode(data);
            var bytes = qrCode.GetGraphic(20);

            return Convert.ToBase64String(bytes);
        }
    }
}
