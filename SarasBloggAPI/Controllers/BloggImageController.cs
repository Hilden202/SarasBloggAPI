using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SarasBloggAPI.DAL;
using SarasBloggAPI.Data;
using SarasBloggAPI.DTOs;
using SarasBloggAPI.Models;
using SarasBloggAPI.Services;
using Microsoft.AspNetCore.Authorization;

namespace SarasBloggAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = "AdminOrSuperadmin")]
    public class BloggImageController : ControllerBase
    {
        private static readonly SemaphoreSlim _gitHubGate = new(1, 1); // serialisera writes lite försiktigt

        private readonly BloggImageManager _imageManager;
        private readonly IFileHelper _fileHelper;
        private readonly MyDbContext _context;
        private readonly ILogger<BloggImageController> _logger;

        public BloggImageController(
            BloggImageManager imageManager,
            IFileHelper fileHelper,
            MyDbContext context,
            ILogger<BloggImageController> logger)
        {
            _imageManager = imageManager;
            _fileHelper = fileHelper;
            _context = context;
            _logger = logger;
        }

        [AllowAnonymous]
        [HttpGet("blogg/{bloggId}")]
        public async Task<ActionResult<IEnumerable<BloggImageDto>>> GetImagesByBloggId(int bloggId)
        {
            var images = await _imageManager.GetImagesByBloggIdAsync(bloggId);

            var imageDtos = images.Select(img => new BloggImageDto
            {
                Id = img.Id,
                BloggId = img.BloggId,
                FilePath = img.FilePath,
                Order = img.Order
            });

            return Ok(imageDtos);
        }

        [HttpPost("upload")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(25 * 1024 * 1024)]                         // 25 MB övergräns
        [RequestFormLimits(MultipartBodyLengthLimit = 25 * 1024 * 1024)]
        public async Task<IActionResult> UploadImage([FromForm] BloggImageUploadDto dto)
        {
            if (dto.File == null || dto.File.Length == 0)
                return BadRequest("Ingen bild bifogad.");

            // Enkel validering
            const long MaxBytes = 20 * 1024 * 1024; // 20 MB (håll i sync med klienttext)
            var allowedExt = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                              { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
            var allowedMime = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                              { "image/jpeg", "image/png", "image/webp", "image/gif" };

            var ext = Path.GetExtension(dto.File.FileName);
            if (string.IsNullOrWhiteSpace(ext) || !allowedExt.Contains(ext))
                return BadRequest($"Endast .jpg, .jpeg, .png, .webp, .gif tillåts. Fil: {dto.File.FileName}");

            if (!allowedMime.Contains(dto.File.ContentType))
            {
                if (string.Equals(dto.File.ContentType, "image/heic", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(dto.File.ContentType, "image/heif", StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest("HEIC/HEIF stöds inte. Välj JPEG/PNG/WebP (i iPhone: Kamera > Format > 'Mest kompatibel').");
                }
                return BadRequest($"Ogiltig MIME-typ: {dto.File.ContentType}. Fil: {dto.File.FileName}");
            }

            if (dto.File.Length > MaxBytes)
                return BadRequest($"Filen är för stor ({dto.File.Length / (1024 * 1024)} MB). Max 20 MB. Fil: {dto.File.FileName}");

            var bloggExists = await _context.Bloggs.AnyAsync(b => b.Id == dto.BloggId);
            if (!bloggExists)
                return BadRequest($"Blogg med ID {dto.BloggId} finns inte.");

            try
            {
                // Serialisera GitHub-writes: minskar 403/422/409 p.g.a. sekundär rate-limit/edge-cases
                await _gitHubGate.WaitAsync();
                string? imageUrl = null;
                try
                {
                    imageUrl = await _fileHelper.SaveImageAsync(dto.File, dto.BloggId, "blogg");
                }
                finally
                {
                    _gitHubGate.Release();
                }

                if (string.IsNullOrWhiteSpace(imageUrl))
                    return StatusCode(500, "Fel vid uppladdning: tom URL från filhjälparen.");

                var image = new BloggImage
                {
                    BloggId = dto.BloggId,
                    FilePath = imageUrl
                };

                // Viktigt: använd manager så Order sätts (max+1)
                await _imageManager.AddImageAsync(image);

                var imageDto = new BloggImageDto
                {
                    Id = image.Id,
                    BloggId = image.BloggId,
                    FilePath = image.FilePath,
                    Order = image.Order
                };

                return CreatedAtAction(nameof(GetImagesByBloggId), new { bloggId = dto.BloggId }, imageDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fel vid uppladdning av bild för blogg {BloggId}", dto.BloggId);
                return StatusCode(500, $"Fel vid uppladdning: {ex.Message}");
            }
        }

        [HttpPut("blogg/{bloggId}/order")]
        public async Task<IActionResult> UpdateImageOrder(int bloggId, [FromBody] List<BloggImageDto> images)
        {
            if (images == null || images.Count == 0)
                return BadRequest("Ingen bildlista mottogs.");

            var existingImages = await _context.BloggImages
                .Where(i => i.BloggId == bloggId)
                .ToListAsync();

            var dirty = false;
            for (int i = 0; i < images.Count; i++)
            {
                var dto = images[i];
                var dbImage = existingImages.FirstOrDefault(img => img.Id == dto.Id);
                if (dbImage != null && dbImage.Order != i)
                {
                    dbImage.Order = i;
                    dirty = true;
                }
            }

            if (dirty)
                await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteImage(int id)
        {
            var image = await _imageManager.GetImageByIdAsync(id);
            if (image == null)
                return NotFound("Bild hittades inte.");

            try
            {
                await _fileHelper.DeleteImageAsync(image.FilePath, "blogg"); // mappen är "blogg" under uploads/
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Kunde inte radera fil på GitHub för imageId {Id}", id);
                // Vi fortsätter ändå och tar bort DB-raden nedan
            }

            await _imageManager.DeleteImageAsync(id);
            return NoContent();
        }

        [HttpDelete("blogg/{bloggId}")]
        public async Task<IActionResult> DeleteImagesByBloggId(int bloggId)
        {
            try
            {
                await _fileHelper.DeleteBlogFolderAsync(bloggId, "blogg");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Kunde inte radera GitHub-mapp för blogg {BloggId}", bloggId);
                // Fortsätt ändå med DB-rensning
            }

            await _imageManager.DeleteImagesByBloggIdAsync(bloggId);
            return NoContent();
        }
    }
}
