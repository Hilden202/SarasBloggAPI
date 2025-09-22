using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SarasBloggAPI.DAL;
using SarasBloggAPI.DTOs;
using SarasBloggAPI.Services;
using Microsoft.AspNetCore.Identity;
using SarasBloggAPI.Data;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http.HttpResults;
using SarasBloggAPI.Models;
using System.Data;
using System.Xml.Linq;
using System.Reflection.Metadata;

namespace SarasBloggAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UserController : ControllerBase
    {
        private readonly UserManagerService _userManagerService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly MyDbContext _db;

        public UserController(UserManagerService userManagerService,
                              UserManager<ApplicationUser> userManager,
                              SignInManager<ApplicationUser> signInManager,
                              MyDbContext db)
        {
            _userManagerService = userManagerService;
            _userManager = userManager;
            _signInManager = signInManager;
            _db = db;
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
            roleName = roleName?.Trim().ToLowerInvariant();
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
            roleName = roleName?.Trim().ToLowerInvariant();
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

            // Förhindra dubblett av användarnamn
            var exists = await _userManager.FindByNameAsync(dto.NewUserName);
            if (exists is not null && exists.Id != id)
                return BadRequest(new BasicResultDto { Succeeded = false, Message = "Username already in use." });

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

            var exists = await _userManager.FindByNameAsync(dto.NewUserName);
            if (exists is not null && exists.Id != myId)
                return BadRequest(new BasicResultDto { Succeeded = false, Message = "Username already in use." });

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

            // --- Sanity/normalisering ---
            if (dto.BirthYear is < 1900 or > 2100)
                dto.BirthYear = null;
            if (dto.Name != null)
                dto.Name = dto.Name.Trim();

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

            if (dto.NotifyOnNewPost.HasValue && dto.NotifyOnNewPost.Value != user.NotifyOnNewPost)
            {
                user.NotifyOnNewPost = dto.NotifyOnNewPost.Value;
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

        [Authorize]
        [HttpGet("me/personal-data")]
        [HttpGet("~/api/users/me/personal-data")]
        [ProducesResponseType(typeof(PersonalDataDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetMyPersonalData()
        {
            var myId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(myId)) return Unauthorized();

            var user = await _userManager.FindByIdAsync(myId);
            if (user is null) return Unauthorized();

            var roles = await _userManager.GetRolesAsync(user);

            // === Extra: hämta relaterade kommentarer & likes ===
            // RÄTT: använd den injicerade DbContexten i kontrollern
            var comments = await _db.Comments
                .Where(c => c.UserId == myId)
                .OrderBy(c => c.CreatedAt)
                .Select(c => new { c.Id, c.BloggId, c.Content, c.CreatedAt })
                .ToListAsync();

            var likes = await _db.BloggLikes
                .Where(l => l.UserId == myId)
                .OrderBy(l => l.CreatedAt)
                .Select(l => new { l.Id, l.BloggId, l.CreatedAt })
                .ToListAsync();

            var bloggIds = comments.Select(c => c.BloggId)
                .Concat(likes.Select(l => l.BloggId))
                .Distinct()
                .ToList();

            var bloggTitles = await _db.Bloggs
                .Where(b => bloggIds.Contains(b.Id))
                .Select(b => new { b.Id, b.Title })
                .ToDictionaryAsync(x => x.Id, x => x.Title ?? "");

            var data = new Dictionary<string, string?>
            {
                ["Id"] = user.Id,
                ["UserName"] = user.UserName,
                ["Email"] = user.Email,
                ["PhoneNumber"] = await _userManager.GetPhoneNumberAsync(user),
                ["Name"] = user.Name,
                ["BirthYear"] = user.BirthYear?.ToString(),
                ["TwoFactorEnabled"] = user.TwoFactorEnabled.ToString(),
                ["LockoutEnd"] = user.LockoutEnd?.UtcDateTime.ToString("O"),
                ["AccessFailedCount"] = user.AccessFailedCount.ToString(),
                ["NotifyOnNewPost"] = user.NotifyOnNewPost.ToString()
            };

            var claims = User.Claims
                .Select(c => new KeyValuePair<string, string>(c.Type, c.Value))
                .ToList();

            List<SarasBloggAPI.DTOs.CommentPreviewDto> commentDtos =
                comments.Select(c => new SarasBloggAPI.DTOs.CommentPreviewDto
                {
                    Id = Convert.ToInt32(c.Id),
                    BloggId = Convert.ToInt32(c.BloggId),
                    BloggTitle = bloggTitles.TryGetValue(Convert.ToInt32(c.BloggId), out var t1) ? t1 : "",
                    Content = c.Content,
                    CreatedAt = c.CreatedAt
                }).ToList();

            List<SarasBloggAPI.DTOs.LikePreviewDto> likeDtos =
                likes.Select(l => new SarasBloggAPI.DTOs.LikePreviewDto
                {
                    Id = Convert.ToInt32(l.Id),
                    BloggId = Convert.ToInt32(l.BloggId),
                    BloggTitle = bloggTitles.TryGetValue(Convert.ToInt32(l.BloggId), out var t2) ? t2 : "",
                    CreatedAt = l.CreatedAt
                }).ToList();


            // Bygg svaret – sätt räknare från listorna
            var dto = new SarasBloggAPI.DTOs.PersonalDataDto
            {
                Data = data,
                Roles = roles.ToList(),
                Claims = claims,
                Comments = commentDtos,
                Likes = likeDtos,
                CommentsCount = commentDtos.Count, // property på List<T>
                LikesCount = likeDtos.Count
            };

            return Ok(dto);

        }

        [Authorize]
        [HttpGet("me/personal-data/download")]
        [HttpGet("~/api/users/me/personal-data/download")]
        public async Task<IActionResult> DownloadMyPersonalData()
        {
            var myId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(myId)) return Unauthorized();
            var blob = await _userManagerService.BuildPersonalDataFileAsync(myId);
            if (blob is null) return Unauthorized();
            return File(blob.Value.Bytes, blob.Value.ContentType, blob.Value.FileName);
        }

        [Authorize]
        [HttpDelete("me")]
        [HttpDelete("~/api/users/me")]
        [Consumes("application/json")]
        [ProducesResponseType(typeof(BasicResultDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(BasicResultDto), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> DeleteMe([FromBody] DeleteMeRequestDto dto)
        {
            var myId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(myId)) return Unauthorized();

            var user = await _userManager.FindByIdAsync(myId);
            if (user is null) return BadRequest(new BasicResultDto { Succeeded = false, Message = "User not found." });

            if ((user.Email ?? "").Equals("admin@sarasblogg.se", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new BasicResultDto { Succeeded = false, Message = "System user cannot be deleted." });

            // Kräv lösenord om användaren har ett
            var hasPassword = await _userManager.HasPasswordAsync(user);
            if (hasPassword)
            {
                if (string.IsNullOrWhiteSpace(dto?.Password))
                    return BadRequest(new BasicResultDto { Succeeded = false, Message = "Password required." });

                var ok = await _userManager.CheckPasswordAsync(user, dto.Password);
                if (!ok)
                    return BadRequest(new BasicResultDto { Succeeded = false, Message = "Invalid password." });
            }

            var res = await _userManager.DeleteAsync(user);
            if (!res.Succeeded)
            {
                var err = string.Join("; ", res.Errors.Select(e => e.Description));
                return BadRequest(new BasicResultDto { Succeeded = false, Message = err });
            }

            await _signInManager.SignOutAsync();
            return Ok(new BasicResultDto { Succeeded = true, Message = "Account deleted." });
        }

    }
}
