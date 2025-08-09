using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SarasBloggAPI.DAL;
using SarasBloggAPI.Data;
using SarasBloggAPI.DTOs;
using SarasBloggAPI.Models;
using SarasBloggAPI.Services;

namespace SarasBloggAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BloggImageController : ControllerBase
    {
        private readonly BloggImageManager _imageManager;
        private readonly IFileHelper _fileHelper;
        private readonly MyDbContext _context;

        public BloggImageController(BloggImageManager imageManager, IFileHelper fileHelper, MyDbContext context)
        {
            _imageManager = imageManager;
            _fileHelper = fileHelper;
            _context = context;
        }

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
        public async Task<IActionResult> UploadImage([FromForm] BloggImageUploadDto dto)
        {
            if (dto.File == null || dto.File.Length == 0)
                return BadRequest("Ingen bild bifogad.");

            // ✅ Enkel validering: filtyp + MIME + storlek
            const long MaxBytes = 5 * 1024 * 1024; // 5 MB
            var allowedExt = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { ".jpg", ".jpeg", ".png", ".webp", ".gif" };

            var allowedMime = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "image/jpeg", "image/png", "image/webp", "image/gif" };

            var ext = Path.GetExtension(dto.File.FileName);
            if (string.IsNullOrWhiteSpace(ext) || !allowedExt.Contains(ext))
                return BadRequest("Endast bildfiler (.jpg, .jpeg, .png, .webp, .gif) tillåts.");

            if (!allowedMime.Contains(dto.File.ContentType))
                return BadRequest("Ogiltig MIME-typ för bild.");

            if (dto.File.Length > MaxBytes)
                return BadRequest("Filen är för stor. Max 5 MB.");


            var bloggExists = await _context.Bloggs.AnyAsync(b => b.Id == dto.BloggId);
            if (!bloggExists)
                return BadRequest($"Blogg med ID {dto.BloggId} finns inte.");

            try
            {
                var imageUrl = await _fileHelper.SaveImageAsync(dto.File, dto.BloggId, "blogg");

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
                return StatusCode(500, $"Fel vid uppladdning: {ex.Message}");
            }
        }

        [HttpPut("blogg/{bloggId}/order")]
        public async Task<IActionResult> UpdateImageOrder(int bloggId, [FromBody] List<BloggImageDto> images)
        {
            if (images == null || !images.Any())
                return BadRequest("Ingen bildlista mottogs.");

            var existingImages = await _context.BloggImages
                .Where(i => i.BloggId == bloggId)
                .ToListAsync();

            for (int i = 0; i < images.Count; i++)
            {
                var dto = images[i];
                var dbImage = existingImages.FirstOrDefault(img => img.Id == dto.Id);
                if (dbImage != null)
                    dbImage.Order = i;
            }

            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteImage(int id)
        {
            var image = await _imageManager.GetImageByIdAsync(id);
            if (image == null)
                return NotFound("Bild hittades inte.");

            await _fileHelper.DeleteImageAsync(image.FilePath, "uploads");
            await _imageManager.DeleteImageAsync(id);

            return NoContent();
        }

        [HttpDelete("blogg/{bloggId}")]
        public async Task<IActionResult> DeleteImagesByBloggId(int bloggId)
        {
            await _fileHelper.DeleteBlogFolderAsync(bloggId, "blogg");
            await _imageManager.DeleteImagesByBloggIdAsync(bloggId);
            return NoContent();
        }
    }
}