using Microsoft.AspNetCore.Mvc;
using SarasBloggAPI.DAL;
using SarasBloggAPI.Models;

namespace SarasBloggAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AboutMeController : ControllerBase
    {
        private readonly AboutMeManager _manager;

        public AboutMeController(AboutMeManager manager)
        {
            _manager = manager;
        }

        [HttpGet]
        public async Task<ActionResult<AboutMe?>> GetAboutMe()
        {
            var aboutMe = await _manager.GetAsync();
            if (aboutMe == null)
                return NotFound();

            return Ok(aboutMe);
        }

        [HttpPost]
        public async Task<ActionResult<AboutMe>> CreateAboutMe(AboutMe aboutMe)
        {
            var created = await _manager.CreateAsync(aboutMe);
            return CreatedAtAction(nameof(GetAboutMe), new { id = created.Id }, created);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateAboutMe(int id, AboutMe aboutMe)
        {
            if (id != aboutMe.Id)
                return BadRequest("Id mismatch");

            var success = await _manager.UpdateAsync(aboutMe);
            if (!success)
                return NotFound();

            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteAboutMe(int id)
        {
            var success = await _manager.DeleteAsync(id);
            if (!success)
                return NotFound();

            return NoContent();
        }
    }
}
