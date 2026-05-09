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
    [Authorize(Policy = ApiTokenAuthenticationExtensions.WebUserOnlyPolicy)]
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
        public async Task<IActionResult> GetMyTenant(CancellationToken cancellationToken)
        {
            var tenants = await _tenantStorageService.GetTenantsAsync(cancellationToken);
            var tenant = tenants.FirstOrDefault();
            if (tenant is null)
                return NotFound();

            return Ok(tenant);
        }

        [HttpGet("tenants/{tenantId:guid}")]
        [ProducesResponseType(typeof(TenantDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
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
        [Authorize(Policy = ApiTokenAuthenticationExtensions.AdminOnlyPolicy)]
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

        [HttpPut("tenants/{tenantId:guid}")]
        [Authorize(Policy = ApiTokenAuthenticationExtensions.AdminOnlyPolicy)]
        [ProducesResponseType(typeof(TenantDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [TenantAccess(TenantAccessMode.AdminOnly, TenantPermission.Admin)]
        public async Task<IActionResult> UpdateTenant(
            Guid tenantId,
            [FromBody] UpdateTenantRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                var result = await _tenantStorageService.UpdateTenantAsync(tenantId, request, cancellationToken);
                return result is null ? NotFound() : Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Access denied while updating tenant {TenantId}.", tenantId);
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

        [HttpDelete("tenants/{tenantId:guid}")]
        [Authorize(Policy = ApiTokenAuthenticationExtensions.AdminOnlyPolicy)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [TenantAccess(TenantAccessMode.AdminOnly, TenantPermission.Admin)]
        public async Task<IActionResult> DeleteTenant(Guid tenantId, CancellationToken cancellationToken)
        {
            try
            {
                var result = await _tenantStorageService.DeleteTenantAsync(tenantId, cancellationToken);
                if (!result)
                    return NotFound();

                await QueueDeletedTenantCleanupTaskAsync(cancellationToken);
                return NoContent();
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Access denied while deleting tenant {TenantId}.", tenantId);
                return Forbid();
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { error = ex.Message });
            }
        }

        [HttpPost("tenants/{tenantId:guid}/disable")]
        [Authorize(Policy = ApiTokenAuthenticationExtensions.AdminOnlyPolicy)]
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
        public Task<IActionResult> GetTenantTokensLegacy(Guid tenantId, CancellationToken cancellationToken)
        {
            return GetTenantTokens(tenantId, cancellationToken);
        }

        [HttpGet("users/tenants")]
        [Authorize(Policy = ApiTokenAuthenticationExtensions.AdminOnlyPolicy)]
        [ProducesResponseType(typeof(IReadOnlyList<UserTenantsDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetUsersWithTenants(CancellationToken cancellationToken)
        {
            try
            {
                var result = await _tenantStorageService.GetUsersWithTenantsAsync(cancellationToken);
                return Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Access denied while getting users with tenants.");
                return Forbid();
            }
        }

        [HttpGet("users")]
        [Authorize(Policy = ApiTokenAuthenticationExtensions.AdminOnlyPolicy)]
        [ProducesResponseType(typeof(IReadOnlyList<UserManagementDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetUsers(CancellationToken cancellationToken)
        {
            try
            {
                var result = await _tenantStorageService.GetUsersAsync(cancellationToken);
                return Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Access denied while getting users.");
                return Forbid();
            }
        }

        [HttpPost("users")]
        [Authorize(Policy = ApiTokenAuthenticationExtensions.AdminOnlyPolicy)]
        [ProducesResponseType(typeof(UserManagementDto), StatusCodes.Status201Created)]
        public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var result = await _tenantStorageService.CreateUserAsync(request, cancellationToken);
                return StatusCode(StatusCodes.Status201Created, result);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Access denied while creating user.");
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

        [HttpPut("users/{userId:guid}")]
        [Authorize(Policy = ApiTokenAuthenticationExtensions.AdminOnlyPolicy)]
        [ProducesResponseType(typeof(UserManagementDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateUser(
            Guid userId,
            [FromBody] UpdateUserRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                var result = await _tenantStorageService.UpdateUserAsync(userId, request, cancellationToken);
                return result is null ? NotFound() : Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Access denied while updating user {UserId}.", userId);
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

        [HttpPost("users/{userId:guid}/block")]
        [Authorize(Policy = ApiTokenAuthenticationExtensions.AdminOnlyPolicy)]
        public async Task<IActionResult> BlockUser(Guid userId, CancellationToken cancellationToken)
        {
            try
            {
                var result = await _tenantStorageService.SetUserBlockedAsync(userId, true, cancellationToken);
                return result ? NoContent() : NotFound();
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Access denied while blocking user {UserId}.", userId);
                return Forbid();
            }
        }

        [HttpPost("users/{userId:guid}/unblock")]
        [Authorize(Policy = ApiTokenAuthenticationExtensions.AdminOnlyPolicy)]
        public async Task<IActionResult> UnblockUser(Guid userId, CancellationToken cancellationToken)
        {
            try
            {
                var result = await _tenantStorageService.SetUserBlockedAsync(userId, false, cancellationToken);
                return result ? NoContent() : NotFound();
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Access denied while unblocking user {UserId}.", userId);
                return Forbid();
            }
        }

        [HttpDelete("users/{userId:guid}")]
        [Authorize(Policy = ApiTokenAuthenticationExtensions.AdminOnlyPolicy)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteUser(Guid userId, CancellationToken cancellationToken)
        {
            try
            {
                var result = await _tenantStorageService.DeleteUserAsync(userId, cancellationToken);
                if (!result)
                    return NotFound();

                await QueueDeletedTenantCleanupTaskAsync(cancellationToken);
                return NoContent();
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Access denied while deleting user {UserId}.", userId);
                return Forbid();
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { error = ex.Message });
            }
        }

        [HttpPost("api-tokens")]
        [Authorize(Policy = ApiTokenAuthenticationExtensions.AdminOnlyPolicy)]
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
        [Authorize(Policy = ApiTokenAuthenticationExtensions.AdminOnlyPolicy)]
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

        [HttpPut("api-tokens/{tokenId:guid}")]
        [Authorize(Policy = ApiTokenAuthenticationExtensions.AdminOnlyPolicy)]
        [ProducesResponseType(typeof(ApiTokenDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [TenantAccess(TenantAccessMode.AdminOnly, TenantPermission.Admin)]
        public async Task<IActionResult> UpdateToken(
            Guid tokenId,
            [FromBody] UpdateApiTokenRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                var result = await _tenantStorageService.UpdateApiTokenAsync(tokenId, request, cancellationToken);
                return result is null
                    ? NotFound(ApiErrorResponse.Create(HttpContext, "FileNotFound", "API token was not found."))
                    : Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Access denied while updating API token {TokenId}.", tokenId);
                return Forbid();
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiErrorResponse.Create(HttpContext, "ValidationError", ex.Message));
            }
        }

        [HttpPost("api-tokens/{tokenId:guid}/disable")]
        [Authorize(Policy = ApiTokenAuthenticationExtensions.AdminOnlyPolicy)]
        [TenantAccess(TenantAccessMode.AdminOnly, TenantPermission.Admin)]
        public async Task<IActionResult> DisableToken(Guid tokenId, CancellationToken cancellationToken)
        {
            var result = await _tenantStorageService.RevokeApiTokenAsync(tokenId, cancellationToken);
            return result
                ? NoContent()
                : NotFound(ApiErrorResponse.Create(HttpContext, "FileNotFound", "API token was not found."));
        }

        [HttpPost("api-tokens/{tokenId:guid}/rotate")]
        [Authorize(Policy = ApiTokenAuthenticationExtensions.AdminOnlyPolicy)]
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

        [HttpPost("storage/cleanup-deleted-tenants")]
        [TenantAccess(TenantAccessMode.AdminOnly, TenantPermission.Admin)]
        public async Task<IActionResult> QueueDeletedTenantCleanup(CancellationToken cancellationToken)
        {
            var task = await QueueDeletedTenantCleanupTaskAsync(cancellationToken);

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

        private async Task<FileStorageBackgroundTask> QueueDeletedTenantCleanupTaskAsync(CancellationToken cancellationToken)
        {
            var task = FileStorageBackgroundTask.CleanupDeletedTenantFiles();
            await _backgroundTaskQueue.QueueAsync(task, cancellationToken);

            _logger.LogInformation(
                "Deleted tenant cleanup queued. TaskId={TaskId}",
                task.TaskId);

            return task;
        }
    }
}
