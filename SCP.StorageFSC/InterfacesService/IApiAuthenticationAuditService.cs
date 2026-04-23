using scp.filestorage.Data.Dto;

namespace SCP.StorageFSC.InterfacesService
{
    public interface IApiAuthenticationAuditService
    {
        Task LogSuccessAsync(HttpContext context, ApiTokenValidationResult result, CancellationToken cancellationToken = default);
        Task LogFailureAsync(HttpContext context, string errorMessage, CancellationToken cancellationToken = default);
        Task LogForbiddenAsync(HttpContext context, CancellationToken cancellationToken = default);
    }
}
