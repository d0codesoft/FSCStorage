using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SCP.StorageFSC.Data.Dto;
using SCP.StorageFSC.InterfacesService;

namespace SCP.StorageFSC.Controllers
{
    [ApiController]
    [Route("api/file")]
    [Authorize]
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
        [HttpPost("upload")]
        [RequestSizeLimit(long.MaxValue)]
        [RequestFormLimits(MultipartBodyLengthLimit = long.MaxValue)]
        public async Task<IActionResult> Upload(
            IFormFile file,
            [FromForm] string? category,
            [FromForm] string? externalKey,
            CancellationToken cancellationToken)
        {
            if (file == null || file.Length == 0)
                return BadRequest("File is required.");

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
                SaveFileStatus.ValidationError => BadRequest(result),
                SaveFileStatus.AccessDenied => Forbid(),
                SaveFileStatus.DuplicateFile => Conflict(result),
                _ => StatusCode(500, result)
            };
        }

        /// <summary>
        /// Get all tenant files
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<StoredTenantFileDto>>> GetFiles(
            CancellationToken cancellationToken)
        {
            var files = await _fileStorageService.GetFilesAsync(cancellationToken);
            return Ok(files);
        }

        /// <summary>
        /// Get file info by guid
        /// </summary>
        [HttpGet("{fileGuid:guid}")]
        public async Task<ActionResult<StoredTenantFileDto>> GetFileInfo(
            Guid fileGuid,
            CancellationToken cancellationToken)
        {
            var file = await _fileStorageService.GetFileInfoAsync(
                fileGuid,
                cancellationToken);

            if (file == null)
                return NotFound();

            return Ok(file);
        }

        /// <summary>
        /// Download file content
        /// </summary>
        [HttpGet("{fileGuid:guid}/download")]
        public async Task<IActionResult> Download(
            Guid fileGuid,
            CancellationToken cancellationToken)
        {
            var result = await _fileStorageService.OpenReadAsync(
                fileGuid,
                cancellationToken);

            if (result == null)
                return NotFound();

            return File(
                result.Content,
                result.File.ContentType ?? "application/octet-stream",
                result.File.FileName);
        }

        /// <summary>
        /// Delete tenant file
        /// </summary>
        [HttpDelete("{fileGuid:guid}")]
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
                return NotFound();

            return NoContent();
        }

        /// <summary>
        /// Cleanup orphan physical files (admin only)
        /// </summary>
        [HttpPost("cleanup-orphans")]
        public async Task<ActionResult<int>> CleanupOrphans(
            CancellationToken cancellationToken)
        {
            var deletedCount = await _fileStorageService.DeleteOrphanFilesAsync(
                cancellationToken);

            return Ok(new
            {
                DeletedCount = deletedCount
            });
        }
    }
}
