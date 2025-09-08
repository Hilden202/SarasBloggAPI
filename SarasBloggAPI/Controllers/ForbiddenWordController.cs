using Microsoft.AspNetCore.Mvc;
using SarasBloggAPI.DAL;
using SarasBloggAPI.Models;
using Microsoft.AspNetCore.Authorization;

namespace SarasBloggAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = "CanModerateComments")]
    public class ForbiddenWordController : ControllerBase
    {
        private readonly ForbiddenWordManager _manager;

        public ForbiddenWordController(ForbiddenWordManager manager)
        {
            _manager = manager;
        }

        // GET: api/forbiddenword
        [HttpGet]
        public async Task<ActionResult<List<ForbiddenWord>>> GetAll()
        {
            var words = await _manager.GetAllAsync();
            return Ok(words);
        }

        // POST: api/forbiddenword
        [HttpPost]
        public async Task<ActionResult<ForbiddenWord>> Create([FromBody] ForbiddenWord word)
        {
            var created = await _manager.CreateAsync(word);
            return CreatedAtAction(nameof(GetAll), new { id = created.Id }, created);
        }

        // DELETE: api/forbiddenword/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var result = await _manager.DeleteAsync(id);
            return result ? NoContent() : NotFound();
        }
    }
}
