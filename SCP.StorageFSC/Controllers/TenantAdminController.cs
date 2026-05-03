using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using SCP.StorageFSC.Data.Models;
using SCP.StorageFSC.Data.Repositories;
using scp.filestorage.Services;
using SCP.StorageFSC.Data.Dto;
using SCP.StorageFSC.InterfacesService;
using SCP.StorageFSC.Security;
using SCP.StorageFSC.SecurityPermission;

namespace SCP.StorageFSC.Controllers
{
    [ApiController]
    [Route("ui-api")]
    [Authorize(Policy = ApiTokenAuthenticationExtensions.AdminOnlyPolicy)]
    public class TenantAdminController : Controller
    {
        private readonly ITenantStorageService _tenantStorageService;
        private readonly IFileStorageService _fileStorageService;
        private readonly IFileStorageBackgroundTaskQueue _backgroundTaskQueue;
        private readonly IBackgroundTaskRepository _backgroundTaskRepository;
        private readonly IStorageStatisticsRepository _storageStatisticsRepository;
        private readonly ILogger<TenantAdminController> _logger;
        private readonly ICurrentTenantAccessor _currentTenantAccessor;

        public TenantAdminController(
            ITenantStorageService tenantStorageService,
            IFileStorageService fileStorageService,
            IFileStorageBackgroundTaskQueue backgroundTaskQueue,
            IBackgroundTaskRepository backgroundTaskRepository,
            IStorageStatisticsRepository storageStatisticsRepository,
            ICurrentTenantAccessor currentTenantAccessor,
            ILogger<TenantAdminController> logger)
        {
            _tenantStorageService = tenantStorageService;
            _fileStorageService = fileStorageService;
            _backgroundTaskQueue = backgroundTaskQueue;
            _backgroundTaskRepository = backgroundTaskRepository;
            _storageStatisticsRepository = storageStatisticsRepository;
            _logger = logger;
            _currentTenantAccessor = currentTenantAccessor;
        }

        [HttpGet("tenants")]
        [ProducesResponseType(typeof(IReadOnlyList<TenantDto>), StatusCodes.Status200OK)]
        [TenantAccess(TenantAccessMode.AdminOnly, TenantPermission.Admin)]
        public async Task<IActionResult> GetTenants(CancellationToken cancellationToken)
        {
            try
            {
                var result = await _tenantStorageService.GetTenantsAsync(cancellationToken);
                return Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Access denied while getting tenants.");
                return Forbid();
            }
        }
        
        [HttpGet("tenant/me")]
        [HttpGet("tenants/me")]
        [Authorize(Policy = ApiTokenAuthenticationExtensions.WebUserOnlyPolicy)]
        [TenantAccess(TenantAccessMode.Authenticated, TenantPermission.Read)]
        public async Task<IActionResult> GetMyTenant(CancellationToken cancellationToken)
        {
            var current = _currentTenantAccessor.GetRequired();

            if (!current.TenantId.HasValue)
                return NotFound();

            var tenant = await _tenantStorageService.GetTenantByIdAsync(current.TenantId.Value, cancellationToken);
            return tenant is null ? NotFound() : Ok(tenant);
        }

        [HttpGet("tenants/{tenantId:guid}")]
        [ProducesResponseType(typeof(TenantDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [TenantAccess(TenantAccessMode.AdminOnly, TenantPermission.Admin)]
        public async Task<IActionResult> GetTenantById(Guid tenantId, CancellationToken cancellationToken)
        {
            try
            {
                var result = await _tenantStorageService.GetTenantByIdAsync(tenantId, cancellationToken);
                if (result is null)
                    return NotFound();

                return Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Access denied while getting tenant {TenantId}.", tenantId);
                return Forbid();
            }
        }

        [HttpPost("tenants")]
        [ProducesResponseType(typeof(TenantDto), StatusCodes.Status201Created)]
        [TenantAccess(TenantAccessMode.AdminOnly, TenantPermission.Admin)]
        public async Task<IActionResult> CreateTenant(
            [FromBody] CreateTenantRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                var result = await _tenantStorageService.CreateTenantAsync(request, cancellationToken);

                return CreatedAtAction(
                    nameof(GetTenantById),
                    new { tenantId = result.Id },
                    result);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Access denied while creating tenant.");
                return Forbid();
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { error = ex.Message });
            }
        }

        [HttpPost("tenants/{tenantId:guid}/disable")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [TenantAccess(TenantAccessMode.AdminOnly, TenantPermission.Admin)]
        public async Task<IActionResult> DisableTenant(Guid tenantId, CancellationToken cancellationToken)
        {
            try
            {
                var result = await _tenantStorageService.DisableTenantAsync(tenantId, cancellationToken);
                if (!result)
                    return NotFound();

                return NoContent();
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Access denied while disabling tenant {TenantId}.", tenantId);
                return Forbid();
            }
        }

        [HttpGet("api-tokens")]
        [ProducesResponseType(typeof(IReadOnlyList<ApiTokenDto>), StatusCodes.Status200OK)]
        [TenantAccess(TenantAccessMode.AdminOnly, TenantPermission.Admin)]
        public async Task<IActionResult> GetTenantTokens(
            [FromQuery] Guid tenantId,
            CancellationToken cancellationToken)
        {
            try
            {
                var result = await _tenantStorageService.GetTenantTokensAsync(tenantId, cancellationToken);
                return Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Access denied while getting tokens for tenant {TenantId}.", tenantId);
                return Forbid();
            }
        }

        [HttpGet("tenants/{tenantId:guid}/api-tokens")]
        [ApiExplorerSettings(IgnoreApi = true)]
        [TenantAccess(TenantAccessMode.AdminOnly, TenantPermission.Admin)]
        public Task<IActionResult> GetTenantTokensLegacy(Guid tenantId, CancellationToken cancellationToken)
        {
            return GetTenantTokens(tenantId, cancellationToken);
        }

        [HttpPost("api-tokens")]
        [ProducesResponseType(typeof(CreatedApiTokenResponse), StatusCodes.Status201Created)]
        [TenantAccess(TenantAccessMode.AdminOnly, TenantPermission.Admin)]
        public async Task<IActionResult> CreateToken(
            [FromBody] CreateApiTokenRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                var result = await _tenantStorageService.CreateApiTokenAsync(request, cancellationToken);
                return StatusCode(StatusCodes.Status201Created, CreatedApiTokenResponse.FromResult(result));
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Access denied while creating API token.");
                return Forbid();
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiErrorResponse.Create(HttpContext, "ValidationError", ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(ApiErrorResponse.Create(HttpContext, "TenantNotFound", ex.Message));
            }
        }

        [HttpDelete("api-tokens/{tokenId:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [TenantAccess(TenantAccessMode.AdminOnly, TenantPermission.Admin)]
        public async Task<IActionResult> DeleteToken(Guid tokenId, CancellationToken cancellationToken)
        {
            try
            {
                var result = await _tenantStorageService.DeleteApiTokenAsync(tokenId, cancellationToken);
                if (!result)
                    return NotFound(ApiErrorResponse.Create(HttpContext, "FileNotFound", "API token was not found."));

                return NoContent();
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Access denied while revoking token {TokenId}.", tokenId);
                return Forbid();
            }
        }

        [HttpPost("api-tokens/{tokenId:guid}/disable")]
        [TenantAccess(TenantAccessMode.AdminOnly, TenantPermission.Admin)]
        public async Task<IActionResult> DisableToken(Guid tokenId, CancellationToken cancellationToken)
        {
            var result = await _tenantStorageService.RevokeApiTokenAsync(tokenId, cancellationToken);
            return result
                ? NoContent()
                : NotFound(ApiErrorResponse.Create(HttpContext, "FileNotFound", "API token was not found."));
        }

        [HttpPost("api-tokens/{tokenId:guid}/rotate")]
        [ProducesResponseType(typeof(CreatedApiTokenResponse), StatusCodes.Status201Created)]
        [TenantAccess(TenantAccessMode.AdminOnly, TenantPermission.Admin)]
        public async Task<IActionResult> RotateToken(Guid tokenId, CancellationToken cancellationToken)
        {
            try
            {
                var result = await _tenantStorageService.RotateApiTokenAsync(tokenId, cancellationToken);
                return result is null
                    ? NotFound(ApiErrorResponse.Create(HttpContext, "FileNotFound", "API token was not found."))
                    : StatusCode(StatusCodes.Status201Created, CreatedApiTokenResponse.FromResult(result));
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(ApiErrorResponse.Create(HttpContext, "Conflict", ex.Message));
            }
        }

        [HttpPost("tokens/{tokenId:guid}/revoke")]
        [ApiExplorerSettings(IgnoreApi = true)]
        [TenantAccess(TenantAccessMode.AdminOnly, TenantPermission.Admin)]
        public Task<IActionResult> RevokeTokenLegacy(Guid tokenId, CancellationToken cancellationToken)
        {
            return DisableToken(tokenId, cancellationToken);
        }

        [HttpPost("storage/check-consistency")]
        [TenantAccess(TenantAccessMode.AdminOnly, TenantPermission.Admin)]
        public async Task<IActionResult> QueueFileStorageConsistencyCheck(CancellationToken cancellationToken)
        {
            var task = FileStorageBackgroundTask.CheckDatabaseConsistency();

            await _backgroundTaskQueue.QueueAsync(task, cancellationToken);

            _logger.LogInformation(
                "File storage consistency check queued by admin request. TaskId={TaskId}",
                task.TaskId);

            return Accepted(new
            {
                task.TaskId,
                task.Type,
                task.CreatedAtUtc,
                Status = BackgroundTaskStatus.Queued,
                StatusName = BackgroundTaskStatus.Queued.ToString()
            });
        }

        [HttpPost("system/cleanup-orphans")]
        [TenantAccess(TenantAccessMode.AdminOnly, TenantPermission.Admin)]
        public async Task<ActionResult<int>> CleanupOrphans(
            CancellationToken cancellationToken)
        {
            var deletedCount = await _fileStorageService.DeleteOrphanFilesAsync(cancellationToken);

            return Ok(new
            {
                DeletedCount = deletedCount
            });
        }

        [HttpGet("storage/statistics")]
        [ProducesResponseType(typeof(StorageStatisticsDto), StatusCodes.Status200OK)]
        [TenantAccess(TenantAccessMode.AdminOnly, TenantPermission.Admin)]
        public async Task<IActionResult> GetStorageStatistics(
            [FromQuery] int largestFilesLimit = 25,
            CancellationToken cancellationToken = default)
        {
            var result = await _storageStatisticsRepository.GetAsync(largestFilesLimit, cancellationToken);
            return Ok(result);
        }

        [HttpGet("storage/tasks/active")]
        [ProducesResponseType(typeof(IReadOnlyList<BackgroundTaskDto>), StatusCodes.Status200OK)]
        [TenantAccess(TenantAccessMode.AdminOnly, TenantPermission.Admin)]
        public async Task<IActionResult> GetActiveBackgroundTasks(CancellationToken cancellationToken)
        {
            var tasks = await _backgroundTaskRepository.GetActiveAsync(cancellationToken);
            return Ok(tasks.Select(ToDto).ToArray());
        }

        [HttpGet("storage/tasks/completed")]
        [ProducesResponseType(typeof(IReadOnlyList<BackgroundTaskDto>), StatusCodes.Status200OK)]
        [TenantAccess(TenantAccessMode.AdminOnly, TenantPermission.Admin)]
        public async Task<IActionResult> GetCompletedBackgroundTasks(
            [FromQuery] int limit = 100,
            CancellationToken cancellationToken = default)
        {
            var tasks = await _backgroundTaskRepository.GetCompletedAsync(limit, cancellationToken);
            return Ok(tasks.Select(ToDto).ToArray());
        }

        [HttpGet("storage/tasks/{taskId:guid}")]
        [ProducesResponseType(typeof(BackgroundTaskDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [TenantAccess(TenantAccessMode.AdminOnly, TenantPermission.Admin)]
        public async Task<IActionResult> GetBackgroundTask(Guid taskId, CancellationToken cancellationToken)
        {
            var task = await _backgroundTaskRepository.GetByTaskIdAsync(taskId, cancellationToken);
            return task is null ? NotFound() : Ok(ToDto(task));
        }

        private static BackgroundTaskDto ToDto(BackgroundTask task)
        {
            var typeName = Enum.IsDefined(typeof(FileStorageBackgroundTaskType), (int)task.Type)
                ? ((FileStorageBackgroundTaskType)task.Type).ToString()
                : $"Unknown({task.Type})";

            return new BackgroundTaskDto(
                task.TaskId,
                task.Type,
                typeName,
                task.Status,
                task.Status.ToString(),
                task.UploadId,
                task.QueuedAtUtc,
                task.StartedAtUtc,
                task.CompletedAtUtc,
                task.FailedAtUtc,
                task.ErrorMessage,
                task.ResultSummary);
        }
    }
}
