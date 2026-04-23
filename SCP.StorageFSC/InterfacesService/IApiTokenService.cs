using scp.filestorage.Data.Dto;

namespace scp.filestorage.InterfacesService
{
    public interface IApiTokenService
    {
        Task<ApiTokenValidationResult?> ValidateAsync(
            string presentedToken,
            CancellationToken cancellationToken = default);
    }
}
