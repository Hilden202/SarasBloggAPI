using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SarasBloggAPI.DAL;
using SarasBloggAPI.DTOs;
using SarasBloggAPI.Services;

namespace SarasBloggAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UserController : ControllerBase
    {
        private readonly UserManagerService _userManagerService;

        public UserController(UserManagerService userManagerService)
        {
            _userManagerService = userManagerService;
        }

        [Authorize(Policy = "AdminOrSuperadmin")]
        [HttpGet("all")]
        [ProducesResponseType(typeof(IEnumerable<UserDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetAllUsers()
        {
            var users = await _userManagerService.GetAllUsersAsync();
            return Ok(users);
        }

        [Authorize(Policy = "AdminOrSuperadmin")]
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetUserById(string id)
        {
            var user = await _userManagerService.GetUserByIdAsync(id);
            return user == null ? NotFound() : Ok(user);
        }

        [Authorize(Policy = "AdminOrSuperadmin")]
        [HttpGet("{id}/roles")]
        [ProducesResponseType(typeof(IEnumerable<string>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetUserRoles(string id)
        {
            var roles = await _userManagerService.GetUserRolesAsync(id);
            return Ok(roles);
        }

        [Authorize(Policy = "SuperadminOnly")]
        [HttpDelete("delete/{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> DeleteUser(string id)
        {
            var user = await _userManagerService.GetUserByIdAsync(id);
            if (user == null)
                return NotFound();

            if (user.Email.ToLower() == "admin@sarasblogg.se")
                return BadRequest("❌ Denna användare kan inte tas bort.");

            var result = await _userManagerService.DeleteUserAsync(id);
            return result ? Ok() : BadRequest("❌ Borttagning misslyckades.");
        }

        [Authorize(Policy = "SuperadminOnly")]
        [HttpPost("{id}/add-role/{roleName}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> AddRole(string id, string roleName)
        {
            var success = await _userManagerService.AddUserToRoleAsync(id, roleName);
            return success ? Ok() : BadRequest("❌ Kunde inte lägga till rollen.");
        }

        [Authorize(Policy = "SuperadminOnly")]
        [HttpDelete("{id}/remove-role/{roleName}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> RemoveRole(string id, string roleName)
        {
            var user = await _userManagerService.GetUserByIdAsync(id);
            if (user?.Email?.ToLower() == "admin@sarasblogg.se")
                return BadRequest("❌ Det går inte att ta bort roller från admin@sarasblogg.se.");

            var success = await _userManagerService.RemoveUserFromRoleAsync(id, roleName);
            return success ? Ok() : BadRequest("❌ Kunde inte ta bort rollen.");
        }

        [Authorize(Policy = "SuperadminOnly")]
        [HttpPut("{id}/username")]
        [ProducesResponseType(typeof(BasicResultDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(BasicResultDto), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> ChangeUserName(string id, [FromBody] ChangeUserNameRequestDto dto)
        {
            if (dto is null || string.IsNullOrWhiteSpace(dto.NewUserName))
                return BadRequest(new BasicResultDto { Succeeded = false, Message = "New username is required." });

            var result = await _userManagerService.ChangeUserNameAsync(id, dto.NewUserName);
            return result.Succeeded ? Ok(result) : BadRequest(result);
        }

        [Authorize(Policy = "RequireUser")]
        [HttpPut("me/username")]
        [ProducesResponseType(typeof(BasicResultDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(BasicResultDto), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> ChangeMyUserName([FromBody] ChangeUserNameRequestDto dto)
        {
            if (dto is null || string.IsNullOrWhiteSpace(dto.NewUserName))
                return BadRequest(new BasicResultDto { Succeeded = false, Message = "New username is required." });

            var myId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(myId))
                return Unauthorized();

            var result = await _userManagerService.ChangeUserNameAsync(myId, dto.NewUserName);
            return result.Succeeded ? Ok(result) : BadRequest(result);
        }

    }
}
