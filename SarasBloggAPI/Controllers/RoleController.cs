using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace SarasBloggAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = "SuperadminOnly")]
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

            roleName = roleName.ToLowerInvariant(); // konsekvent skiftläge
            if (await _roleManager.RoleExistsAsync(roleName))
                return Ok(); // redan finns

            var result = await _roleManager.CreateAsync(new IdentityRole(roleName));
            return result.Succeeded ? Ok() : BadRequest(result.Errors);
        }

        [HttpDelete("delete/{roleName}")]
        public async Task<IActionResult> DeleteRole(string roleName)
        {
            roleName = roleName?.Trim();
            if (string.IsNullOrWhiteSpace(roleName))
                return BadRequest("Rollnamn krävs.");

            var normalized = roleName.ToLowerInvariant();

            // Skydda grundroller
            var protectedRoles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    { "superadmin", "admin", "user", "superuser" };
            if (protectedRoles.Contains(normalized))
                return BadRequest($"Rollen '{normalized}' kan inte tas bort.");

            var role = await _roleManager.FindByNameAsync(normalized);
            if (role == null)
                return NotFound($"Rollen '{normalized}' finns inte.");

            var result = await _roleManager.DeleteAsync(role);
            return result.Succeeded ? Ok() : BadRequest(result.Errors);
        }


    }
}
