using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SarasBloggAPI.DTOs;
using SarasBloggAPI.Services;
using Swashbuckle.AspNetCore.Annotations;

namespace SarasBloggAPI.Controllers
{
    [ApiController]
    [Route("api/editor")]
    [Authorize(Policy = "CanManageBlogs")]
    public class EditorUploadController : ControllerBase
    {
        private readonly IFileHelper _fileHelper;
        private readonly ILogger<EditorUploadController> _logger;
        private static readonly SemaphoreSlim _gate = new(1, 1); // säkert vid samtidiga writes

        public EditorUploadController(IFileHelper fileHelper, ILogger<EditorUploadController> logger)
        {
            _fileHelper = fileHelper;
            _logger = logger;
        }

        [HttpPost("upload-image")]
        [RequestFormLimits(MultipartBodyLengthLimit = 10 * 1024 * 1024)]
        [RequestSizeLimit(10 * 1024 * 1024)]
        [Consumes("multipart/form-data")]
        [SwaggerOperation(Summary = "Ladda upp inbäddad TinyMCE-bild")]
        public async Task<IActionResult> Upload([FromForm] EditorImageUploadDto dto, [FromQuery] int bloggId = 0)
        {
            var file = dto.File;
            if (file == null || file.Length == 0)
                return BadRequest("Ingen fil bifogad.");

            var allowedExt = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { ".jpg", ".jpeg", ".png", ".webp" };
            var allowedMime = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { "image/jpeg", "image/png", "image/webp" };

            var ext = Path.GetExtension(file.FileName);
            if (!allowedExt.Contains(ext))
                return BadRequest("Endast .jpg, .jpeg, .png, .webp tillåts.");

            if (!allowedMime.Contains(file.ContentType))
                return BadRequest("Ogiltig MIME-typ: " + file.ContentType);

            try
            {
                await _gate.WaitAsync();
                string? imageUrl;

                if (bloggId > 0)
                {
                    // ?? Spara i blogg/{id}/text/
                    var folderName = Path.Combine("blogg", bloggId.ToString(), "text");
                    imageUrl = await _fileHelper.SaveImageAsync(file, bloggId, folderName);
                }
                else
                {
                    // ?? fallback: t.ex. "Om mig" (utan bloggId)
                    imageUrl = await _fileHelper.SaveImageAsync(file, "editor");
                }

                if (string.IsNullOrWhiteSpace(imageUrl))
                    return StatusCode(500, "Fel vid uppladdning: tom URL.");

                return Ok(new { location = imageUrl });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fel vid TinyMCE-uppladdning.");
                return StatusCode(500, "Fel vid uppladdning: " + ex.Message);
            }
            finally
            {
                _gate.Release();
            }
        }
    }
}
