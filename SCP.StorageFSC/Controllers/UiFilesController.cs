using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SCP.StorageFSC.Data.Dto;
using SCP.StorageFSC.InterfacesService;
using SCP.StorageFSC.Security;
using SCP.StorageFSC.SecurityPermission;

namespace SCP.StorageFSC.Controllers
{
    [ApiController]
    [Route("ui-api/files")]
    [Authorize(Policy = ApiTokenAuthenticationExtensions.WebUserOnlyPolicy)]
    public sealed class UiFilesController : ControllerBase
    {
        private readonly IFileStorageService _fileStorageService;

        public UiFilesController(IFileStorageService fileStorageService)
        {
            _fileStorageService = fileStorageService;
        }

        [HttpGet]
        [TenantAccess(TenantAccessMode.Authenticated, TenantPermission.Read)]
        public async Task<ActionResult<IReadOnlyList<StoredTenantFileDto>>> GetFiles(
            CancellationToken cancellationToken)
        {
            var files = await _fileStorageService.GetFilesAsync(cancellationToken);
            return Ok(files);
        }

        [HttpGet("{fileGuid:guid}/metadata")]
        [TenantAccess(TenantAccessMode.Authenticated, TenantPermission.Read)]
        public async Task<ActionResult<StoredTenantFileDto>> GetFileInfo(
            Guid fileGuid,
            CancellationToken cancellationToken)
        {
            var file = await _fileStorageService.GetFileInfoAsync(fileGuid, cancellationToken);
            return file is null
                ? NotFound(ApiErrorResponse.Create(HttpContext, "FileNotFound", "File was not found."))
                : Ok(file);
        }

        [HttpDelete("{fileGuid:guid}")]
        [TenantAccess(TenantAccessMode.Authenticated, TenantPermission.Delete)]
        public async Task<IActionResult> Delete(
            Guid fileGuid,
            CancellationToken cancellationToken)
        {
            var deleted = await _fileStorageService.DeleteFileAsync(fileGuid, cancellationToken);
            return deleted
                ? NoContent()
                : NotFound(ApiErrorResponse.Create(HttpContext, "FileNotFound", "File was not found."));
        }
    }
}
