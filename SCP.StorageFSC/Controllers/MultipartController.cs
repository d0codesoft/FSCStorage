using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using scp.filestorage.Data.Dto;
using scp.filestorage.InterfacesService;
using SCP.StorageFSC.Data.Dto;
using SCP.StorageFSC.Security;
using SCP.StorageFSC.SecurityPermission;

namespace scp.filestorage.Controllers
{
    [ApiController]
    [Route("api/multipart")]
    [Authorize(Policy = ApiTokenAuthenticationExtensions.ApiTokenOnlyPolicy)]
    [TenantAccess(TenantAccessMode.Authenticated)]
    public sealed class MultipartController : ControllerBase
    {
        private readonly IFileStorageMultipartService _multipartService;
        private readonly ILogger<MultipartController> _logger;

        public MultipartController(
            IFileStorageMultipartService multipartService,
            ILogger<MultipartController> logger)
        {
            _multipartService = multipartService;
            _logger = logger;
        }

        /// <summary>
        /// Initializes a multipart upload.
        /// </summary>
        [HttpPost("init")]
        [TenantAccess(TenantAccessMode.Authenticated, TenantPermission.Write)]
        [ProducesResponseType(typeof(InitMultipartUploadResultDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Init(
            [FromBody] InitMultipartUploadRequestDto request,
            CancellationToken cancellationToken)
        {
            try
            {
                var result = await _multipartService.InitAsync(request, cancellationToken);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Multipart init validation failed.");
                return BadRequest(Error("ValidationError", ex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Multipart init failed.");
                return StatusCode(StatusCodes.Status500InternalServerError, Error("StorageFailed", "Multipart initialization failed."));
            }
        }

        [HttpPut("{uploadId:guid}/parts/{partNumber:int}")]
        [TenantAccess(TenantAccessMode.Authenticated, TenantPermission.Write)]
        [RequestSizeLimit(long.MaxValue)]
        [RequestFormLimits(MultipartBodyLengthLimit = long.MaxValue)]
        [ProducesResponseType(typeof(UploadMultipartPartResultDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UploadPartByRoute(
            Guid uploadId,
            int partNumber,
            [FromForm] string? partChecksumSha256,
            [FromForm(Name = "file")] IFormFile file,
            CancellationToken cancellationToken)
        {
            if (file is null)
                return BadRequest(Error("ValidationError", "File is required."));

            if (file.Length <= 0)
                return BadRequest(Error("ValidationError", "File is empty."));

            try
            {
                await using var stream = file.OpenReadStream();

                var request = new UploadMultipartPartRequestDto
                {
                    UploadId = uploadId,
                    PartNumber = partNumber,
                    Content = stream,
                    ContentLength = file.Length,
                    PartChecksumSha256 = partChecksumSha256
                };

                var result = await _multipartService.UploadPartAsync(request, cancellationToken);
                return Ok(result);
            }
            catch (FileNotFoundException ex)
            {
                _logger.LogWarning(ex, "Multipart session not found. UploadId={UploadId}", uploadId);
                return NotFound(Error("FileNotFound", ex.Message));
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Multipart part validation failed. UploadId={UploadId}, PartNumber={PartNumber}", uploadId, partNumber);
                return BadRequest(Error("ValidationError", ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Multipart part upload rejected. UploadId={UploadId}, PartNumber={PartNumber}", uploadId, partNumber);
                return Conflict(Error("Conflict", ex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Multipart part upload failed. UploadId={UploadId}, PartNumber={PartNumber}", uploadId, partNumber);
                return StatusCode(StatusCodes.Status500InternalServerError, Error("StorageFailed", "Multipart part upload failed."));
            }
        }

        /// <summary>
        /// Uploads a single part.
        /// multipart/form-data:
        /// - uploadId
        /// - partNumber
        /// - partChecksumSha256 (optional)
        /// - file
        /// </summary>
        [HttpPost("part")]
        [ApiExplorerSettings(IgnoreApi = true)]
        [TenantAccess(TenantAccessMode.Authenticated, TenantPermission.Write)]
        [RequestSizeLimit(long.MaxValue)]
        [RequestFormLimits(MultipartBodyLengthLimit = long.MaxValue)]
        [ProducesResponseType(typeof(UploadMultipartPartResultDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UploadPart(
            [FromForm] Guid uploadId,
            [FromForm] int partNumber,
            [FromForm] string? partChecksumSha256,
            [FromForm(Name = "file")] IFormFile file,
            CancellationToken cancellationToken)
        {
            if (file is null)
                return BadRequest(Error("ValidationError", "File is required."));

            if (file.Length <= 0)
                return BadRequest(Error("ValidationError", "File is empty."));

            try
            {
                await using var stream = file.OpenReadStream();

                var request = new UploadMultipartPartRequestDto
                {
                    UploadId = uploadId,
                    PartNumber = partNumber,
                    Content = stream,
                    ContentLength = file.Length,
                    PartChecksumSha256 = partChecksumSha256
                };

                var result = await _multipartService.UploadPartAsync(request, cancellationToken);
                return Ok(result);
            }
            catch (FileNotFoundException ex)
            {
                _logger.LogWarning(ex, "Multipart session not found. UploadId={UploadId}", uploadId);
                return NotFound(Error("FileNotFound", ex.Message));
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Multipart part validation failed. UploadId={UploadId}, PartNumber={PartNumber}", uploadId, partNumber);
                return BadRequest(Error("ValidationError", ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Multipart part upload rejected. UploadId={UploadId}, PartNumber={PartNumber}", uploadId, partNumber);
                return Conflict(Error("Conflict", ex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Multipart part upload failed. UploadId={UploadId}, PartNumber={PartNumber}", uploadId, partNumber);
                return StatusCode(StatusCodes.Status500InternalServerError, Error("StorageFailed", "Multipart part upload failed."));
            }
        }

        /// <summary>
        /// Gets multipart upload status.
        /// </summary>
        [HttpGet("{uploadId:guid}/status")]
        [TenantAccess(TenantAccessMode.Authenticated, TenantPermission.Read)]
        [ProducesResponseType(typeof(MultipartUploadStatusDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetStatus(
            Guid uploadId,
            CancellationToken cancellationToken)
        {
            try
            {
                var result = await _multipartService.GetStatusAsync(uploadId, cancellationToken);
                if (result is null)
                    return NotFound(Error("FileNotFound", "Multipart upload was not found."));

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get multipart status. UploadId={UploadId}", uploadId);
                return StatusCode(StatusCodes.Status500InternalServerError, Error("DatabaseFailed", "Failed to get multipart upload status."));
            }
        }

        /// <summary>
        /// Completes the multipart upload and assembles the file.
        /// </summary>
        [HttpPost("{uploadId:guid}/complete")]
        [TenantAccess(TenantAccessMode.Authenticated, TenantPermission.Write)]
        [ProducesResponseType(typeof(CompleteMultipartUploadResultDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Complete(
            Guid uploadId,
            CancellationToken cancellationToken)
        {
            var request = new CompleteMultipartUploadRequestDto { UploadId = uploadId };

            try
            {
                var result = await _multipartService.CompleteAsync(request, cancellationToken);
                return Ok(result);
            }
            catch (FileNotFoundException ex)
            {
                _logger.LogWarning(ex, "Multipart session not found during complete. UploadId={UploadId}", request.UploadId);
                return NotFound(Error("FileNotFound", ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Multipart complete rejected. UploadId={UploadId}", request.UploadId);
                return Conflict(Error("Conflict", ex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Multipart complete failed. UploadId={UploadId}", request.UploadId);
                return StatusCode(StatusCodes.Status500InternalServerError, Error("StorageFailed", "Multipart complete failed."));
            }
        }

        [HttpPost("complete")]
        [ApiExplorerSettings(IgnoreApi = true)]
        [TenantAccess(TenantAccessMode.Authenticated, TenantPermission.Write)]
        public Task<IActionResult> CompleteLegacy(
            [FromBody] CompleteMultipartUploadRequestDto request,
            CancellationToken cancellationToken)
        {
            return Complete(request.UploadId, cancellationToken);
        }

        /// <summary>
        /// Aborts the multipart upload.
        /// </summary>
        [HttpDelete("{uploadId:guid}/abort")]
        [TenantAccess(TenantAccessMode.Authenticated, TenantPermission.Write)]
        [ProducesResponseType(typeof(AbortMultipartUploadResultDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Abort(
            Guid uploadId,
            CancellationToken cancellationToken)
        {
            try
            {
                var result = await _multipartService.AbortAsync(uploadId, cancellationToken);
                return Ok(result);
            }
            catch (FileNotFoundException ex)
            {
                _logger.LogWarning(ex, "Multipart session not found during abort. UploadId={UploadId}", uploadId);
                return NotFound(Error("FileNotFound", ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Multipart abort rejected. UploadId={UploadId}", uploadId);
                return Conflict(Error("Conflict", ex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Multipart abort failed. UploadId={UploadId}", uploadId);
                return StatusCode(StatusCodes.Status500InternalServerError, Error("StorageFailed", "Multipart abort failed."));
            }
        }

        [HttpPost("{uploadId:guid}/abort")]
        [ApiExplorerSettings(IgnoreApi = true)]
        [TenantAccess(TenantAccessMode.Authenticated, TenantPermission.Write)]
        public Task<IActionResult> AbortLegacy(
            Guid uploadId,
            CancellationToken cancellationToken)
        {
            return Abort(uploadId, cancellationToken);
        }

        private ApiErrorResponse Error(string errorCode, string message) =>
            ApiErrorResponse.Create(HttpContext, errorCode, message);
    }
}
