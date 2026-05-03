using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SCP.StorageFSC.Data.Dto;
using SCP.StorageFSC.InterfacesService;
using SCP.StorageFSC.Security;
using SCP.StorageFSC.SecurityPermission;

namespace SCP.StorageFSC.Controllers
{
    [ApiController]
    [Route("api/files")]
    [Authorize(Policy = ApiTokenAuthenticationExtensions.ApiTokenOnlyPolicy)]
    [TenantAccess(TenantAccessMode.Authenticated)]
    public sealed class FileStorageController : Controller
    {
        private readonly IFileStorageService _fileStorageService;
        private readonly ILogger<FileStorageController> _logger;

        public FileStorageController(
            IFileStorageService fileStorageService,
            ILogger<FileStorageController> logger)
        {
            _fileStorageService = fileStorageService;
            _logger = logger;
        }

        /// <summary>
        /// Upload file for current tenant
        /// </summary>
        [HttpPost]
        [TenantAccess(TenantAccessMode.Authenticated, TenantPermission.Write)]
        [RequestSizeLimit(long.MaxValue)]
        [RequestFormLimits(MultipartBodyLengthLimit = long.MaxValue)]
        public async Task<IActionResult> Upload(
            [FromForm(Name = "file")] IFormFile file,
            [FromForm] string? category,
            [FromForm] string? externalKey,
            CancellationToken cancellationToken)
        {
            if (file == null || file.Length == 0)
                return BadRequest(Error("ValidationError", "File is required."));

            await using var stream = file.OpenReadStream();

            var request = new SaveFileRequest
            {
                FileName = file.FileName,
                Category = category,
                ExternalKey = externalKey,
                ContentType = file.ContentType,
                Content = stream
            };

            var result = await _fileStorageService.SaveFileAsync(
                request,
                cancellationToken);

            if (result.Success)
                return Ok(result);

            return result.Status switch
            {
                SaveFileStatus.ValidationError => BadRequest(Error("ValidationError", result.ErrorMessage ?? "File upload validation failed.")),
                SaveFileStatus.AccessDenied => StatusCode(StatusCodes.Status403Forbidden, Error("AccessDenied", result.ErrorMessage ?? "Access denied.")),
                SaveFileStatus.DuplicateFile => Conflict(Error("DuplicateFile", result.ErrorMessage ?? "A file with the same key already exists.")),
                _ => StatusCode(StatusCodes.Status500InternalServerError, Error("StorageFailed", result.ErrorMessage ?? "File upload failed."))
            };
        }

        /// <summary>
        /// Get all tenant files
        /// </summary>
        [HttpGet]
        [TenantAccess(TenantAccessMode.Authenticated, TenantPermission.Read)]
        public async Task<ActionResult<IReadOnlyList<StoredTenantFileDto>>> GetFiles(
            CancellationToken cancellationToken)
        {
            var files = await _fileStorageService.GetFilesAsync(cancellationToken);
            return Ok(files);
        }

        /// <summary>
        /// Get file info by guid
        /// </summary>
        [HttpGet("{fileGuid:guid}/metadata")]
        [TenantAccess(TenantAccessMode.Authenticated, TenantPermission.Read)]
        public async Task<ActionResult<StoredTenantFileDto>> GetFileInfo(
            Guid fileGuid,
            CancellationToken cancellationToken)
        {
            var file = await _fileStorageService.GetFileInfoAsync(
                fileGuid,
                cancellationToken);

            if (file == null)
                return NotFound(Error("FileNotFound", "File was not found."));

            return Ok(file);
        }

        /// <summary>
        /// Download file content
        /// </summary>
        [HttpGet("{fileGuid:guid}")]
        [TenantAccess(TenantAccessMode.Authenticated, TenantPermission.Read)]
        public async Task<IActionResult> Download(
            Guid fileGuid,
            CancellationToken cancellationToken)
        {
            var result = await _fileStorageService.OpenReadAsync(
                fileGuid,
                cancellationToken);

            if (result == null)
                return NotFound(Error("FileNotFound", "File was not found."));

            return File(
                result.Content,
                result.File.ContentType ?? "application/octet-stream",
                result.File.FileName);
        }

        /// <summary>
        /// Delete tenant file
        /// </summary>
        [HttpDelete("{fileGuid:guid}")]
        [TenantAccess(TenantAccessMode.Authenticated, TenantPermission.Delete)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(
            Guid fileGuid,
            CancellationToken cancellationToken)
        {
            var deleted = await _fileStorageService.DeleteFileAsync(
                fileGuid,
                cancellationToken);

            if (!deleted)
                return NotFound(Error("FileNotFound", "File was not found."));

            return NoContent();
        }

        private ApiErrorResponse Error(string errorCode, string message) =>
            ApiErrorResponse.Create(HttpContext, errorCode, message);
    }
}
