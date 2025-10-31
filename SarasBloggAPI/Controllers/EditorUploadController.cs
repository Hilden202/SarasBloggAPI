using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SarasBloggAPI.DTOs;
using SarasBloggAPI.Services;
using Swashbuckle.AspNetCore.Annotations;

namespace SarasBloggAPI.Controllers
{
    [ApiController]
    [Route("api/editor")]
    public class EditorUploadController : ControllerBase
    {
        private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "image/png",
            "image/jpeg",
            "image/webp"
        };

        private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".png",
            ".jpg",
            ".jpeg",
            ".webp"
        };

        private const long MaxFileSizeBytes = 10L * 1024 * 1024;

        private readonly IFileHelper _fileHelper;
        private readonly ILogger<EditorUploadController> _logger;

        public EditorUploadController(IFileHelper fileHelper, ILogger<EditorUploadController> logger)
        {
            _fileHelper = fileHelper;
            _logger = logger;
        }

        /// <summary>
        /// Uploads an inline image for the TinyMCE editor.
        /// </summary>
        [HttpPost("upload-image")]
        [Authorize(Policy = "CanManageBlogs")]
        [RequestFormLimits(MultipartBodyLengthLimit = MaxFileSizeBytes)]
        [RequestSizeLimit(MaxFileSizeBytes)]
        [Consumes("multipart/form-data")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(EditorImageUploadResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status415UnsupportedMediaType)]
        [SwaggerOperation(
            Summary = "Upload an inline editor image",
            Description = "Example form-data request:\n\n```\nPOST /api/editor/upload-image\nAuthorization: Bearer <token>\nContent-Type: multipart/form-data\n\nfile: (binary PNG/JPEG/WEBP)\n```\n\nExample response:\n```json\n{ \"location\": \"https://cdn.example.com/media/editor/abc123.webp\" }\n```"
        )]
        public async Task<ActionResult<EditorImageUploadResponse>> Upload([FromForm] IFormFile? file)
        {
            if (file is null)
            {
                return BadRequest(new { error = "File upload is required." });
            }

            if (file.Length == 0)
            {
                return BadRequest(new { error = "The uploaded file is empty." });
            }

            if (file.Length > MaxFileSizeBytes)
            {
                return BadRequest(new { error = "Maximum allowed file size is 10 MB." });
            }

            if (string.IsNullOrWhiteSpace(file.ContentType) || !AllowedContentTypes.Contains(file.ContentType))
            {
                return StatusCode(StatusCodes.Status415UnsupportedMediaType, new { error = "Only PNG, JPEG, or WEBP images are allowed." });
            }

            var extension = Path.GetExtension(file.FileName);
            if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
            {
                return StatusCode(StatusCodes.Status415UnsupportedMediaType, new { error = "Only PNG, JPEG, or WEBP images are allowed." });
            }

            try
            {
                var location = await _fileHelper.SaveImageAsync(file, "editor");

                if (string.IsNullOrWhiteSpace(location))
                {
                    _logger.LogError("File helper returned an empty location for editor upload.");
                    return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Image upload failed." });
                }

                return Ok(new EditorImageUploadResponse(location));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to upload editor image.");
                return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Image upload failed." });
            }
        }
    }
}
