using scp.filestorage.Common;
using scp.filestorage.Services.Auth;
using SCP.StorageFSC.Data.Dto;
using SCP.StorageFSC.Data.Models;
using SCP.StorageFSC.Data.Repositories;
using SCP.StorageFSC.InterfacesService;

namespace SCP.StorageFSC.Services
{
    public sealed class UserAuthenticationAuditService : IUserAuthenticationAuditService
    {
        private readonly IUserAuthenticationAuditLogRepository _auditLogRepository;
        private readonly ILogger<UserAuthenticationAuditService> _logger;

        public UserAuthenticationAuditService(
            IUserAuthenticationAuditLogRepository auditLogRepository,
            ILogger<UserAuthenticationAuditService> logger)
        {
            _auditLogRepository = auditLogRepository;
            _logger = logger;
        }

        public Task LogPasswordLoginAsync(
            HttpContext context,
            string login,
            LoginResult result,
            CancellationToken cancellationToken = default)
        {
            return WriteLogAsync(
                context,
                userId: result.UserId,
                userName: result.UserName,
                login: login,
                eventType: "PasswordLogin",
                status: result.Status.ToString(),
                isSuccess: result.Succeeded,
                failureReason: result.Succeeded ? null : result.Status.ToString(),
                cancellationToken);
        }

        public Task LogTwoFactorAsync(
            HttpContext context,
            VerifyTwoFactorResult result,
            string eventType,
            CancellationToken cancellationToken = default)
        {
            return WriteLogAsync(
                context,
                userId: result.UserId,
                userName: result.UserName,
                login: null,
                eventType: eventType,
                status: result.Status.ToString(),
                isSuccess: result.Succeeded,
                failureReason: result.Succeeded ? null : result.Status.ToString(),
                cancellationToken);
        }

        public async Task<UserAuthenticationNotificationPageDto> GetNotificationsAsync(
            Guid? userId = null,
            int pageNumber = 1,
            int pageSize = 20,
            CancellationToken cancellationToken = default)
        {
            pageNumber = Math.Max(1, pageNumber);
            pageSize = Math.Clamp(pageSize, 1, 200);

            var skip = (pageNumber - 1) * pageSize;
            var totalCount = await _auditLogRepository.CountAsync(userId, cancellationToken);
            var rows = await _auditLogRepository.GetPagedAsync(userId, skip, pageSize, cancellationToken);

            return new UserAuthenticationNotificationPageDto
            {
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalCount = totalCount,
                Items = rows.Select(MapNotification).ToList()
            };
        }

        private async Task WriteLogAsync(
            HttpContext context,
            Guid? userId,
            string? userName,
            string? login,
            string eventType,
            string status,
            bool isSuccess,
            string? failureReason,
            CancellationToken cancellationToken)
        {
            try
            {
                var ipInfo = ClientIpHelper.GetClientIp(context);

                var log = new UserAuthenticationAuditLog
                {
                    CreatedUtc = DateTime.UtcNow,
                    UserId = userId,
                    UserName = string.IsNullOrWhiteSpace(userName) ? null : userName,
                    Login = string.IsNullOrWhiteSpace(login) ? null : login,
                    EventType = eventType,
                    Status = status,
                    IsSuccess = isSuccess,
                    FailureReason = failureReason,
                    ClientIp = ipInfo.Ip,
                    IpSource = ipInfo.Source,
                    ForwardedForRaw = string.IsNullOrWhiteSpace(ipInfo.ForwardedForRaw) ? null : ipInfo.ForwardedForRaw,
                    RealIpRaw = string.IsNullOrWhiteSpace(ipInfo.RealIpRaw) ? null : ipInfo.RealIpRaw,
                    RequestPath = context.Request.Path.Value ?? string.Empty,
                    UserAgent = context.Request.Headers.UserAgent.ToString()
                };

                await _auditLogRepository.InsertAsync(log, cancellationToken);
                _logger.LogInformation("User authentication audit: " + log);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to write user authentication audit log.");
            }
        }

        private static UserAuthenticationNotificationDto MapNotification(UserAuthenticationAuditLog log)
        {
            return new UserAuthenticationNotificationDto
            {
                Id = log.Id,
                UserId = log.UserId,
                UserName = log.UserName,
                Login = log.Login,
                EventType = log.EventType,
                Status = log.Status,
                IsSuccess = log.IsSuccess,
                FailureReason = log.FailureReason,
                ClientIp = log.ClientIp,
                IpSource = log.IpSource,
                ForwardedForRaw = log.ForwardedForRaw,
                RealIpRaw = log.RealIpRaw,
                RequestPath = log.RequestPath,
                UserAgent = log.UserAgent,
                CreatedUtc = log.CreatedUtc
            };
        }
    }
}
