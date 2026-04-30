using Microsoft.AspNetCore.Mvc;
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
    [Route("api/admin")]
    public class TenantAdminController : Controller
    {
        private readonly ITenantStorageService _tenantStorageService;
        private readonly IFileStorageBackgroundTaskQueue _backgroundTaskQueue;
        private readonly IBackgroundTaskRepository _backgroundTaskRepository;
        private readonly ILogger<TenantAdminController> _logger;
        private readonly ICurrentTenantAccessor _currentTenantAccessor;

        public TenantAdminController(
            ITenantStorageService tenantStorageService,
            IFileStorageBackgroundTaskQueue backgroundTaskQueue,
            IBackgroundTaskRepository backgroundTaskRepository,
            ICurrentTenantAccessor currentTenantAccessor,
            ILogger<TenantAdminController> logger)
        {
            _tenantStorageService = tenantStorageService;
            _backgroundTaskQueue = backgroundTaskQueue;
            _backgroundTaskRepository = backgroundTaskRepository;
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

        [HttpGet("tenants/{tenantId:guid}/tokens")]
        [ProducesResponseType(typeof(IReadOnlyList<ApiTokenDto>), StatusCodes.Status200OK)]
        [TenantAccess(TenantAccessMode.Authenticated, TenantPermission.Read)]
        public async Task<IActionResult> GetTenantTokens(Guid tenantId, CancellationToken cancellationToken)
        {
            try
            {
                var current = _currentTenantAccessor.GetRequired();
                if (!current.IsAdmin && current.TenantId != tenantId)
                {
                    _logger.LogWarning("Tenant {TenantId} attempted to access tokens for tenant {TargetTenantId}.", current.TenantId, tenantId);
                    return Forbid();
                }

                var result = await _tenantStorageService.GetTenantTokensAsync(tenantId, cancellationToken);
                return Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Access denied while getting tokens for tenant {TenantId}.", tenantId);
                return Forbid();
            }
        }

        [HttpPost("tokens")]
        [ProducesResponseType(typeof(CreatedApiTokenResult), StatusCodes.Status201Created)]
        [TenantAccess(TenantAccessMode.Authenticated, TenantPermission.Write)]
        public async Task<IActionResult> CreateToken(
            [FromBody] CreateApiTokenRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                var current = _currentTenantAccessor.GetRequired();
                if (!current.IsAdmin && current.TenantId != request.TenantId)
                {
                    _logger.LogWarning("Tenant {TenantId} attempted to create token for tenant {TargetTenantId}.", current.TenantId, request.TenantId);
                    return Forbid();
                }

                var result = await _tenantStorageService.CreateApiTokenAsync(request, cancellationToken);
                return StatusCode(StatusCodes.Status201Created, result);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Access denied while creating API token.");
                return Forbid();
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { error = ex.Message });
            }
        }

        [HttpPost("tokens/{tokenId:guid}/revoke")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [TenantAccess(TenantAccessMode.Authenticated, TenantPermission.Write)]
        public async Task<IActionResult> RevokeToken(Guid tokenId, CancellationToken cancellationToken)
        {
            try
            {
                var current = _currentTenantAccessor.GetRequired();
                if (!current.IsAdmin)
                {
                    var token = await _tenantStorageService.GetApiTokenByIdAsync(tokenId, cancellationToken);
                    if (token is null)
                    {
                        _logger.LogWarning("Token {TokenId} not found for revocation.", tokenId);
                        return NotFound();
                    }
                    if (token.TenantId != current.TenantId)
                    {
                        _logger.LogWarning("Tenant {TenantId} attempted to revoke token {TokenId} belonging to tenant {TargetTenantId}.", current.TenantId, tokenId, token.TenantId);
                        return Forbid();
                    }
                }

                var result = await _tenantStorageService.RevokeApiTokenAsync(tokenId, cancellationToken);
                if (!result)
                    return NotFound();

                return NoContent();
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Access denied while revoking token {TokenId}.", tokenId);
                return Forbid();
            }
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
