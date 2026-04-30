using Microsoft.AspNetCore.Mvc;
using scp.filestorage.Data.Dto;
using scp.filestorage.InterfacesService;

namespace scp.filestorage.Controllers
{
    [ApiController]
    [Route("api/multipart")]
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
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Multipart init failed.");
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    error = "Multipart initialization failed."
                });
            }
        }

        [HttpPut("{uploadId:guid}/parts/{partNumber:int}")]
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
                return BadRequest(new { error = "File is required." });

            if (file.Length <= 0)
                return BadRequest(new { error = "File is empty." });

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
                return NotFound(new { error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Multipart part validation failed. UploadId={UploadId}, PartNumber={PartNumber}", uploadId, partNumber);
                return BadRequest(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Multipart part upload rejected. UploadId={UploadId}, PartNumber={PartNumber}", uploadId, partNumber);
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Multipart part upload failed. UploadId={UploadId}, PartNumber={PartNumber}", uploadId, partNumber);
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    error = "Multipart part upload failed."
                });
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
                return BadRequest(new { error = "File is required." });

            if (file.Length <= 0)
                return BadRequest(new { error = "File is empty." });

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
                return NotFound(new { error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Multipart part validation failed. UploadId={UploadId}, PartNumber={PartNumber}", uploadId, partNumber);
                return BadRequest(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Multipart part upload rejected. UploadId={UploadId}, PartNumber={PartNumber}", uploadId, partNumber);
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Multipart part upload failed. UploadId={UploadId}, PartNumber={PartNumber}", uploadId, partNumber);
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    error = "Multipart part upload failed."
                });
            }
        }

        /// <summary>
        /// Gets multipart upload status.
        /// </summary>
        [HttpGet("{uploadId:guid}/status")]
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
                    return NotFound();

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get multipart status. UploadId={UploadId}", uploadId);
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    error = "Failed to get multipart upload status."
                });
            }
        }

        /// <summary>
        /// Completes the multipart upload and assembles the file.
        /// </summary>
        [HttpPost("complete")]
        [ProducesResponseType(typeof(CompleteMultipartUploadResultDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Complete(
            [FromBody] CompleteMultipartUploadRequestDto request,
            CancellationToken cancellationToken)
        {
            try
            {
                var result = await _multipartService.CompleteAsync(request, cancellationToken);
                return Ok(result);
            }
            catch (FileNotFoundException ex)
            {
                _logger.LogWarning(ex, "Multipart session not found during complete. UploadId={UploadId}", request.UploadId);
                return NotFound(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Multipart complete rejected. UploadId={UploadId}", request.UploadId);
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Multipart complete failed. UploadId={UploadId}", request.UploadId);
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    error = "Multipart complete failed."
                });
            }
        }

        /// <summary>
        /// Aborts the multipart upload.
        /// </summary>
        [HttpPost("{uploadId:guid}/abort")]
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
                return NotFound(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Multipart abort rejected. UploadId={UploadId}", uploadId);
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Multipart abort failed. UploadId={UploadId}", uploadId);
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    error = "Multipart abort failed."
                });
            }
        }
    }
}
