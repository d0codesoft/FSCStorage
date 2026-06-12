using scp.filestorage.Services.Auth;
using SCP.StorageFSC.Data.Dto;

namespace SCP.StorageFSC.InterfacesService
{
    public interface IUserAuthenticationAuditService
    {
        Task LogPasswordLoginAsync(
            HttpContext context,
            string login,
            LoginResult result,
            CancellationToken cancellationToken = default);

        Task LogTwoFactorAsync(
            HttpContext context,
            VerifyTwoFactorResult result,
            string eventType,
            CancellationToken cancellationToken = default);

        Task<UserAuthenticationNotificationPageDto> GetNotificationsAsync(
            Guid? userId = null,
            int pageNumber = 1,
            int pageSize = 20,
            CancellationToken cancellationToken = default);
    }
}
