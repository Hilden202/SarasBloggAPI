using Microsoft.AspNetCore.Mvc;
using SarasBloggAPI.DAL;
using SarasBloggAPI.Models;
using SarasBloggAPI.Services;
using SarasBloggAPI.DTOs;

namespace SarasBloggAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AboutMeController : ControllerBase
    {
        private readonly AboutMeManager _manager;
        private readonly IAboutMeImageService _imgSvc;

        public AboutMeController(AboutMeManager manager, IAboutMeImageService imgSvc)
        {
            _manager = manager;
            _imgSvc = imgSvc;
        }

        [HttpGet]
        public async Task<ActionResult<AboutMe?>> GetAboutMe()
        {
            var aboutMe = await _manager.GetAsync();
            if (aboutMe == null) return NotFound();
            return Ok(aboutMe);
        }

        [HttpPost]
        public async Task<IActionResult> CreateAboutMe([FromBody] AboutMe aboutMe)
        {
            var created = await _manager.CreateAsync(aboutMe);
            return CreatedAtAction(nameof(GetAboutMe), new { id = created.Id }, created);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateAboutMe(int id, [FromBody] AboutMe aboutMe)
        {
            if (id != aboutMe.Id) return BadRequest("Id mismatch");
            var success = await _manager.UpdateAsync(aboutMe);
            return success ? NoContent() : NotFound();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteAboutMe(int id)
        {
            var success = await _manager.DeleteAsync(id);
            return success ? NoContent() : NotFound();
        }

        // ---- Bildendpoints ----

        [HttpGet("image")]
        public async Task<ActionResult<AboutMeImageDto>> GetImage()
        {
            var url = await _imgSvc.GetCurrentUrlAsync();
            return Ok(new AboutMeImageDto { ImageUrl = url });
        }

        [HttpPut("image")]
        [RequestSizeLimit(20_000_000)]
        public async Task<ActionResult<AboutMeImageDto>> PutImage([FromForm] AboutMeImageUploadDto dto)
        {
            if (dto.File is null || dto.File.Length == 0)
                return BadRequest("Ingen bild bifogad.");

            var url = await _imgSvc.UploadOrReplaceAsync(dto.File);
            return Ok(new AboutMeImageDto { ImageUrl = url });
        }

        [HttpDelete("image")]
        public async Task<IActionResult> DeleteImage()
        {
            await _imgSvc.DeleteAsync();
            return NoContent();
        }
    }
}
