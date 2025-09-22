using Microsoft.AspNetCore.Mvc;
using SarasBloggAPI.DAL;
using SarasBloggAPI.Models;
using System;
using Microsoft.AspNetCore.Authorization;
using SarasBloggAPI.Services;


namespace SarasBloggAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")] // => api/blogg
    public class BloggController : ControllerBase
    {
        private readonly BloggManager _BloggManager;
        private readonly NewPostNotifier _notifier;

        public BloggController(BloggManager bloggManager, NewPostNotifier notifier)
        {
            _BloggManager = bloggManager;
            _notifier = notifier;
        }

        // GET: api/blogg
        [AllowAnonymous]
        [HttpGet]
        public async Task<ActionResult<List<Blogg>>> GetAll()
        {
            var bloggs = await _BloggManager.GetAllAsync();
            return Ok(bloggs);
        }

        // GET: api/blogg/5
        [AllowAnonymous]
        [HttpGet("{id}")]
        public async Task<ActionResult<Blogg>> Get(int id)
        {
            var blogg = await _BloggManager.GetByIdAsync(id);
            if (blogg == null)
                return NotFound();

            return Ok(blogg);
        }

        // POST: api/blogg
        [Authorize(Policy = "AdminOrSuperadmin")]
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] Blogg blogg)
        {
            try
            {
                var created = await _BloggManager.CreateAsync(blogg);
                // Skicka mejl om inlägget är publikt direkt
                if (!created.Hidden && !created.IsArchived)
                {
                    _ = _notifier.NotifyAsync(created.Id);
                }
                return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[API] Fel vid skapande av blogg: " + ex.Message);
                return StatusCode(500, ex.Message); // Visa felet i frontend-loggen
            }
        }

        // PUT: api/blogg/5
        [Authorize(Policy = "AdminOrSuperadmin")]
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] Blogg updatedBlogg)
        {
            if (id != updatedBlogg.Id)
                return BadRequest();

            var result = await _BloggManager.UpdateAsync(updatedBlogg);
            return result ? NoContent() : NotFound();
        }

        [Authorize(Policy = "AdminOrSuperadmin")]
        [HttpPatch("{id}/hidden")]
        public async Task<IActionResult> ToggleHidden(int id)
        {
            var b = await _BloggManager.GetByIdAsync(id);
            if (b == null) return NotFound();
            b.Hidden = !b.Hidden;
            var ok = await _BloggManager.UpdateAsync(b);
            return ok ? Ok(new { b.Hidden }) : StatusCode(500, "Update failed");
        }

        [Authorize(Policy = "AdminOrSuperadmin")]
        [HttpPatch("{id}/archived")]
        public async Task<IActionResult> ToggleArchived(int id)
        {
            var b = await _BloggManager.GetByIdAsync(id);
            if (b == null) return NotFound();
            b.IsArchived = !b.IsArchived;
            var ok = await _BloggManager.UpdateAsync(b);
            return ok ? Ok(new { b.IsArchived }) : StatusCode(500, "Update failed");
        }


        // DELETE: api/blogg/5
        [Authorize(Policy = "AdminOrSuperadmin")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var result = await _BloggManager.DeleteAsync(id);
            return result ? NoContent() : NotFound();
        }
    }
}
