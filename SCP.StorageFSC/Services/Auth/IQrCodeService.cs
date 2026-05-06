namespace scp.filestorage.Services.Auth
{
    /// <summary>
    /// Generates QR codes.
    /// </summary>
    public interface IQrCodeService
    {
        /// <summary>
        /// Generates a PNG QR code encoded as Base64 string.
        /// </summary>
        string GeneratePngBase64(string text);
    }
}
