using Microsoft.AspNetCore.Mvc;
using SarasBloggAPI.DAL;
using SarasBloggAPI.Models;

namespace SarasBloggAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ContactMeController : ControllerBase
    {
        private readonly ContactMeManager _manager;

        public ContactMeController(ContactMeManager manager)
        {
            _manager = manager;
        }

        [HttpGet]
        public async Task<ActionResult<List<ContactMe>>> GetAll()
        {
            var contacts = await _manager.GetAllAsync();
            return Ok(contacts);
        }

        [HttpPost]
        public async Task<ActionResult<ContactMe>> Create(ContactMe contact)
        {
            var created = await _manager.CreateAsync(contact);
            return CreatedAtAction(nameof(GetAll), new { id = created.Id }, created);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var success = await _manager.DeleteAsync(id);
            if (!success)
                return NotFound();

            return NoContent();
        }
    }
}
