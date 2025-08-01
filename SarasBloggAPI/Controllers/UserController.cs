﻿using Microsoft.AspNetCore.Mvc;
using SarasBloggAPI.DAL;
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

        [HttpGet("all")]
        public async Task<IActionResult> GetAllUsers()
        {
            var users = await _userManagerService.GetAllUsersAsync();
            return Ok(users);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetUserById(string id)
        {
            var user = await _userManagerService.GetUserByIdAsync(id);
            return user == null ? NotFound() : Ok(user);
        }

        [HttpGet("{id}/roles")]
        public async Task<IActionResult> GetUserRoles(string id)
        {
            var roles = await _userManagerService.GetUserRolesAsync(id);
            return Ok(roles);
        }

        [HttpDelete("delete/{id}")]
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

        [HttpPost("{id}/add-role/{roleName}")]
        public async Task<IActionResult> AddRole(string id, string roleName)
        {
            var success = await _userManagerService.AddUserToRoleAsync(id, roleName);
            return success ? Ok() : BadRequest("❌ Kunde inte lägga till rollen.");
        }

        [HttpDelete("{id}/remove-role/{roleName}")]
        public async Task<IActionResult> RemoveRole(string id, string roleName)
        {
            var user = await _userManagerService.GetUserByIdAsync(id);
            if (user?.Email?.ToLower() == "admin@sarasblogg.se")
                return BadRequest("❌ Det går inte att ta bort roller från admin@sarasblogg.se.");

            var success = await _userManagerService.RemoveUserFromRoleAsync(id, roleName);
            return success ? Ok() : BadRequest("❌ Kunde inte ta bort rollen.");
        }

    }
}
