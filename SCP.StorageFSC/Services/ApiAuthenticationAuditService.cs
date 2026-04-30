using scp.filestorage.Common;
using scp.filestorage.Data.Dto;
using SCP.StorageFSC.Data.Models;
using SCP.StorageFSC.Data.Repositories;
using SCP.StorageFSC.InterfacesService;

namespace SCP.StorageFSC.Services
{
    public sealed class ApiAuthenticationAuditService : IApiAuthenticationAuditService
    {
        private readonly IApiTokenConnectionLogRepository _connectionLogRepository;
        private readonly ITenantRepository _tenantRepository;
        private readonly ILogger<ApiAuthenticationAuditService> _logger;

        public ApiAuthenticationAuditService(
            IApiTokenConnectionLogRepository connectionLogRepository,
            ITenantRepository tenantRepository,
            ILogger<ApiAuthenticationAuditService> logger)
        {
            _connectionLogRepository = connectionLogRepository;
            _tenantRepository = tenantRepository;
            _logger = logger;
        }

        public Task LogSuccessAsync(HttpContext context, ApiTokenValidationResult result, CancellationToken cancellationToken = default)
        {
            return WriteLogAsync(context, result, isSuccess: true, errorMessage: null, cancellationToken);
        }

        public Task LogFailureAsync(HttpContext context, string errorMessage, CancellationToken cancellationToken = default)
        {
            return WriteLogAsync(context, null, isSuccess: false, errorMessage, cancellationToken);
        }

        public Task LogForbiddenAsync(HttpContext context, CancellationToken cancellationToken = default)
        {
            return WriteLogAsync(context, null, isSuccess: false, errorMessage: "Forbidden", cancellationToken);
        }

        private async Task WriteLogAsync(
            HttpContext context,
            ApiTokenValidationResult? result,
            bool isSuccess,
            string? errorMessage,
            CancellationToken cancellationToken)
        {
            try
            {
                var ipInfo = ClientIpHelper.GetClientIp(context);
                var tenant = await ResolveTenantAsync(context, result, cancellationToken);

                var log = new ApiTokenConnectionLog
                {
                    CreatedUtc = DateTime.UtcNow,
                    ApiTokenId = result?.TokenId,
                    TokenName = result?.Name ?? string.Empty,
                    TenantId = tenant?.Id ?? result?.TenantId,
                    ExternalTenantId = tenant?.ExternalTenantId,
                    TenantName = tenant?.Name ?? string.Empty,
                    IsSuccess = isSuccess,
                    ErrorMessage = errorMessage,
                    IsAdmin = result?.IsAdmin ?? false,
                    ClientIp = ipInfo.Ip,
                    IpSource = ipInfo.Source,
                    ForwardedForRaw = string.IsNullOrWhiteSpace(ipInfo.ForwardedForRaw) ? null : ipInfo.ForwardedForRaw,
                    RealIpRaw = string.IsNullOrWhiteSpace(ipInfo.RealIpRaw) ? null : ipInfo.RealIpRaw,
                    RequestPath = context.Request.Path.Value ?? string.Empty,
                    UserAgent = context.Request.Headers.UserAgent.ToString()
                };

                await _connectionLogRepository.InsertAsync(log, cancellationToken);
                _logger.LogInformation("API authentication: " + log);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to write API authentication audit log.");
            }
        }

        private async Task<Tenant?> ResolveTenantAsync(
            HttpContext context,
            ApiTokenValidationResult? result,
            CancellationToken cancellationToken)
        {
            if (result?.TenantId.HasValue == true)
            {
                return await _tenantRepository.GetByIdAsync(result.TenantId.Value, cancellationToken);
            }

            var tenantHeader = context.Request.Headers["X-Tenant-Id"].FirstOrDefault();
            if (Guid.TryParse(tenantHeader, out var tenantGuid))
            {
                return await _tenantRepository.GetByGuidAsync(tenantGuid, cancellationToken);
            }

            return null;
        }
    }
}
