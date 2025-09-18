using Microsoft.AspNetCore.Mvc;
using SarasBloggAPI.DAL;
using SarasBloggAPI.Models;
using SarasBloggAPI.Services;
using SarasBloggAPI.DTOs;
using Microsoft.AspNetCore.Authorization;

namespace SarasBloggAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = "AdminOrSuperadmin")] // standardkrav
    public class AboutMeController : ControllerBase
    {
        private readonly AboutMeManager _manager;
        private readonly IAboutMeImageService _imgSvc;

        public AboutMeController(AboutMeManager manager, IAboutMeImageService imgSvc)
        {
            _manager = manager;
            _imgSvc = imgSvc;
        }

        [AllowAnonymous]
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

        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdateAboutMe(int id, [FromBody] AboutMe aboutMe)
        {
            if (aboutMe == null)
                return BadRequest("Ingen data skickades.");

            // säkerställ att route-id gäller
            aboutMe.Id = id;

            var updated = await _manager.UpdateAsync(aboutMe);
            if (!updated)
                return NotFound($"Ingen AboutMe med Id={id} hittades.");

            return NoContent();
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteAboutMe(int id)
        {
            var success = await _manager.DeleteAsync(id);
            return success ? NoContent() : NotFound();
        }

        // ---- Bildendpoints ----

        [AllowAnonymous]
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
