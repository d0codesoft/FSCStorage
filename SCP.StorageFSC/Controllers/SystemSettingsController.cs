using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SCP.StorageFSC.Data.Dto;
using SCP.StorageFSC.InterfacesService;
using SCP.StorageFSC.Security;
using SCP.StorageFSC.SecurityPermission;

namespace SCP.StorageFSC.Controllers
{
    [ApiController]
    [Route("ui-api/system-settings")]
    [Authorize(Policy = ApiTokenAuthenticationExtensions.AdminOnlyPolicy)]
    [TenantAccess(TenantAccessMode.AdminOnly, TenantPermission.Admin)]
    public sealed class SystemSettingsController : ControllerBase
    {
        private readonly ISystemSettingsService _systemSettingsService;
        private readonly ILogger<SystemSettingsController> _logger;

        public SystemSettingsController(
            ISystemSettingsService systemSettingsService,
            ILogger<SystemSettingsController> logger)
        {
            _systemSettingsService = systemSettingsService;
            _logger = logger;
        }

        [HttpGet]
        [ProducesResponseType(typeof(IReadOnlyList<SystemSettingDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
        {
            try
            {
                var settings = await _systemSettingsService.GetAllAsync(cancellationToken);
                return Ok(settings);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Access denied while getting system settings.");
                return Forbid();
            }
        }

        [HttpGet("{name}")]
        [ProducesResponseType(typeof(SystemSettingDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetByName(
            string name,
            CancellationToken cancellationToken)
        {
            try
            {
                var setting = await _systemSettingsService.GetByNameAsync(name, cancellationToken);
                return setting is null ? NotFound(Error("SettingNotFound", "System setting was not found.")) : Ok(setting);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Access denied while getting system setting {SettingName}.", name);
                return Forbid();
            }
        }

        [HttpPut("{name}")]
        [ProducesResponseType(typeof(SystemSettingDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Update(
            string name,
            [FromBody] UpdateSystemSettingRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                var setting = await _systemSettingsService.UpdateAsync(name, request, cancellationToken);
                return Ok(setting);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Access denied while updating system setting {SettingName}.", name);
                return Forbid();
            }
            catch (ArgumentException ex) when (ex.ParamName == "name")
            {
                return NotFound(Error("SettingNotFound", ex.Message));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(Error("ValidationError", ex.Message));
            }
        }

        private ApiErrorResponse Error(string errorCode, string message) =>
            ApiErrorResponse.Create(HttpContext, errorCode, message);
    }
}
