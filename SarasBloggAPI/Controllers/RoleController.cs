using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace SarasBloggAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RoleController : ControllerBase
    {
        private readonly RoleManager<IdentityRole> _roleManager;

        public RoleController(RoleManager<IdentityRole> roleManager)
        {
            _roleManager = roleManager;
        }

        [HttpGet("all")]
        public IActionResult GetAllRoles()
        {
            var roles = _roleManager.Roles.Select(r => r.Name).ToList();
            return Ok(roles);
        }

        [HttpPost("create/{roleName}")]
        public async Task<IActionResult> CreateRole(string roleName)
        {
            if (string.IsNullOrWhiteSpace(roleName))
                return BadRequest("Rollnamn saknas.");

            if (await _roleManager.RoleExistsAsync(roleName))
                return Ok(); // redan finns

            var result = await _roleManager.CreateAsync(new IdentityRole(roleName));
            return result.Succeeded ? Ok() : BadRequest(result.Errors);
        }

        [HttpDelete("delete/{roleName}")]
        public async Task<IActionResult> DeleteRole(string roleName)
        {
            if (string.IsNullOrWhiteSpace(roleName))
                return BadRequest("Rollnamn krävs.");

            if (roleName.ToLower() == "superadmin")
                return BadRequest("Rollen 'superadmin' kan inte tas bort.");

            var role = await _roleManager.FindByNameAsync(roleName);
            if (role == null)
                return NotFound($"Rollen '{roleName}' finns inte.");

            var result = await _roleManager.DeleteAsync(role);
            return result.Succeeded ? Ok() : BadRequest(result.Errors);
        }


    }
}
