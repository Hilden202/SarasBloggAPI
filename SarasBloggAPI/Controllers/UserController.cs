using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SarasBloggAPI.DAL;
using SarasBloggAPI.DTOs;
using SarasBloggAPI.Services;
using Microsoft.AspNetCore.Identity;
using SarasBloggAPI.Data;

namespace SarasBloggAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UserController : ControllerBase
    {
        private readonly UserManagerService _userManagerService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;

        public UserController(UserManagerService userManagerService,
                              UserManager<ApplicationUser> userManager,
                              SignInManager<ApplicationUser> signInManager)
        {
            _userManagerService = userManagerService;
            _userManager = userManager;
            _signInManager = signInManager;
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
        // ---------- UPDATE MY PROFILE ----------
        [Authorize]
        [HttpPut("me/profile")]
        // Alias under /api/users för konsekvent prefix
        [HttpPut("~/api/users/me/profile")]
        [Consumes("application/json")]
        [ProducesResponseType(typeof(BasicResultDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(BasicResultDto), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<BasicResultDto>> UpdateMyProfile([FromBody] UpdateProfileDto dto)
        {
            var myId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(myId))
                return Unauthorized();

            var user = await _userManager.FindByIdAsync(myId);
            if (user is null)
                return BadRequest(new BasicResultDto { Succeeded = false, Message = "User not found." });

            var changed = false;

            // Telefon via Identity-API (validering/normalisering)
            if (dto.PhoneNumber != null)
            {
                var currentPhone = await _userManager.GetPhoneNumberAsync(user);
                if (!string.Equals(dto.PhoneNumber, currentPhone, StringComparison.Ordinal))
                {
                    var setPhone = await _userManager.SetPhoneNumberAsync(user, dto.PhoneNumber);
                    if (!setPhone.Succeeded)
                    {
                        var err = string.Join("; ", setPhone.Errors.Select(e => e.Description));
                        return BadRequest(new BasicResultDto { Succeeded = false, Message = err });
                    }
                    changed = true;
                }
            }

            // Custom-fält på ApplicationUser
            if (dto.Name != null && !string.Equals(dto.Name, user.Name, StringComparison.Ordinal))
            {
                user.Name = dto.Name;
                changed = true;
            }

            if (dto.BirthYear.HasValue && dto.BirthYear != user.BirthYear)
            {
                user.BirthYear = dto.BirthYear;
                changed = true;
            }

            if (changed)
            {
                var upd = await _userManager.UpdateAsync(user);
                if (!upd.Succeeded)
                {
                    var err = string.Join("; ", upd.Errors.Select(e => e.Description));
                    return BadRequest(new BasicResultDto { Succeeded = false, Message = err });
                }

                await _signInManager.RefreshSignInAsync(user); // ofarligt även med JWT
            }

            return Ok(new BasicResultDto
            {
                Succeeded = true,
                Message = changed ? "Din profil har uppdaterats." : "Inga ändringar att spara."
            });
        }

    }
}
