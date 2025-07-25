using Microsoft.AspNetCore.Mvc;
using SarasBloggAPI.DAL;
using SarasBloggAPI.Models;

namespace SarasBloggAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")] // => api/blogg
    public class BloggController : ControllerBase
    {
        private readonly BloggManager _BloggManager;

        public BloggController(BloggManager bloggManager)
        {
            _BloggManager = bloggManager;
        }

        // GET: api/blogg
        [HttpGet]
        public async Task<ActionResult<List<Blogg>>> GetAll()
        {
            var bloggs = await _BloggManager.GetAllAsync();
            return Ok(bloggs);
        }

        // GET: api/blogg/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Blogg>> Get(int id)
        {
            var blogg = await _BloggManager.GetByIdAsync(id);
            if (blogg == null)
                return NotFound();

            return Ok(blogg);
        }

        // POST: api/blogg
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] Blogg blogg)
        {
            try
            {
                var created = await _BloggManager.CreateAsync(blogg);
                return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[API] Fel vid skapande av blogg: " + ex.Message);
                return StatusCode(500, ex.Message); // Visa felet i frontend-loggen
            }
        }

        // PUT: api/blogg/5
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] Blogg updatedBlogg)
        {
            if (id != updatedBlogg.Id)
                return BadRequest();

            var result = await _BloggManager.UpdateAsync(updatedBlogg);
            return result ? NoContent() : NotFound();
        }

        // DELETE: api/blogg/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var result = await _BloggManager.DeleteAsync(id);
            return result ? NoContent() : NotFound();
        }
    }
}
