using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SarasBloggAPI.DAL;
using SarasBloggAPI.Data;
using SarasBloggAPI.Services;
using SarasBloggAPI.DTOs;

namespace SarasBloggAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CommentController : ControllerBase
    {
        private readonly CommentManager _commentManager;
        private readonly ContentSafetyService _contentSafetyService;
        private readonly UserManager<ApplicationUser> _userManager;

        public CommentController(
            CommentManager commentManager,
            ContentSafetyService contentSafetyService,
            UserManager<ApplicationUser> userManager)
        {
            _commentManager = commentManager;
            _contentSafetyService = contentSafetyService;
            _userManager = userManager;
        }

        // ===== Helpers =====

        private static readonly Dictionary<string, int> RoleRank = new(StringComparer.OrdinalIgnoreCase)
        {
            ["superadmin"] = 0,
            ["admin"] = 1,
            ["superuser"] = 2,
            ["user"] = 3
        };

        private static string? GetTopRole(IList<string> roles)
            => roles?.OrderBy(r => RoleRank.TryGetValue(r ?? "", out var i) ? i : 999).FirstOrDefault();

        private async Task<string?> ResolveTopRoleByEmailAsync(string? email)
        {
            if (string.IsNullOrWhiteSpace(email)) return null;
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null) return null;
            var roles = await _userManager.GetRolesAsync(user);
            return GetTopRole(roles);
        }

        private async Task<string?> ResolveCurrentUserNameByEmailAsync(string? email)
        {
            if (string.IsNullOrWhiteSpace(email)) return null;
            var user = await _userManager.FindByEmailAsync(email);
            return user?.UserName;
        }

        private async Task<CommentDto> ToDtoAsync(Models.Comment c)
        {
            var name = c.Name;
            string? topRole = null;

            // Medlemskommentar: översätt till *aktuellt* username + beräkna topproll
            if (!string.IsNullOrWhiteSpace(c.Email))
            {
                name = await ResolveCurrentUserNameByEmailAsync(c.Email) ?? c.Name;
                topRole = await ResolveTopRoleByEmailAsync(c.Email);
            }

            return new CommentDto
            {
                Id = c.Id ?? 0,
                BloggId = c.BloggId,
                Name = name ?? "",
                Content = c.Content,
                CreatedAt = c.CreatedAt,
                TopRole = topRole
            };
        }

        // ===== Endpoints =====

        // Alla (även utloggade) ska se färger/TopRole
        [AllowAnonymous]
        [HttpGet]
        public async Task<List<CommentDto>> GetAllCommentsAsync()
        {
            var comments = await _commentManager.GetCommentsAsync();
            var list = new List<CommentDto>(comments.Count);
            foreach (var c in comments)
                list.Add(await ToDtoAsync(c));
            return list;
        }

        // Effektivt för detaljsida: hämta per blogg
        [AllowAnonymous]
        [HttpGet("by-blogg/{bloggId:int}")]
        public async Task<List<CommentDto>> GetByBloggAsync(int bloggId)
        {
            var all = await _commentManager.GetCommentsAsync();
            var filtered = all.Where(c => c.BloggId == bloggId).OrderBy(c => c.CreatedAt).ToList();
            var list = new List<CommentDto>(filtered.Count);
            foreach (var c in filtered)
                list.Add(await ToDtoAsync(c));
            return list;
        }

        // Hämta en kommentar
        [AllowAnonymous]
        [HttpGet("ById/{id:int}")]
        public async Task<ActionResult<CommentDto>> GetComment(int id)
        {
            var c = await _commentManager.GetCommentAsync(id);
            if (c == null) return NotFound();
            return await ToDtoAsync(c);
        }

        // Skapa kommentar: tillåt anonym OCH inloggad
        [AllowAnonymous]
        [HttpPost]
        public async Task<IActionResult> PostComment([FromBody] Models.Comment comment)
        {
            try
            {
                bool isNameSafe = await _contentSafetyService.IsContentSafeAsync(comment.Name);
                bool isContentSafe = await _contentSafetyService.IsContentSafeAsync(comment.Content);

                if (!isNameSafe)
                    return BadRequest("Namnet innehåller otillåtet språk.");
                if (!isContentSafe)
                    return BadRequest("Kommentaren bedömdes som osäker och kan inte publiceras.");

                // Om inloggad: koppla e-post + *aktuellt* username
                var myId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!string.IsNullOrWhiteSpace(myId))
                {
                    var me = await _userManager.FindByIdAsync(myId);
                    if (me != null)
                    {
                        comment.Email = me.Email;                     // ägarskap
                        comment.Name = me.UserName ?? comment.Name;   // render-namn
                    }
                }
                else
                {
                    // Anonym: se till att Email INTE råkar bli kvar från tidigare request
                    comment.Email = null;
                }

                await _commentManager.CreateCommentAsync(comment);
                return Ok();
            }
            catch
            {
                return StatusCode(500, "Ett fel inträffade vid hantering av kommentaren.");
            }
        }

        // Ta bort kommentar:
        //  - Ägare (matchar inloggad användares e-post) får ta bort sin egen
        //  - Eller någon med modereringsrätt (CanModerateComments)
        [Authorize] // kräver inloggning för ägarradering; moderators täcks av policyn nedan
        [HttpDelete("ById/{id:int}")]
        public async Task<IActionResult> DeleteComment(int id)
        {
            var existing = await _commentManager.GetCommentAsync(id);
            if (existing == null) return NotFound();

            // Inloggad användare?
            var myId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(myId)) return Forbid();

            var me = await _userManager.FindByIdAsync(myId);
            var isOwner = !string.IsNullOrWhiteSpace(existing.Email) &&
                          string.Equals(existing.Email, me?.Email, StringComparison.OrdinalIgnoreCase);

            if (isOwner)
            {
                await _commentManager.DeleteComment(id);
                return Ok();
            }

            // Inte ägare → kräv modereringsrätt
            var roles = me != null ? await _userManager.GetRolesAsync(me) : Array.Empty<string>();
            var canModerate = roles.Any(r =>
                r.Equals("superuser", StringComparison.OrdinalIgnoreCase) ||
                r.Equals("admin", StringComparison.OrdinalIgnoreCase) ||
                r.Equals("superadmin", StringComparison.OrdinalIgnoreCase));

            if (!canModerate) return Forbid();

            await _commentManager.DeleteComment(id);
            return Ok();
        }

        // Massradera per blogg: endast moderatorer
        [Authorize(Policy = "CanModerateComments")]
        [HttpDelete("ByBlogg/{bloggId:int}")]
        public async Task<IActionResult> DeleteComments(int bloggId)
        {
            await _commentManager.DeleteComments(bloggId);
            return Ok();
        }
    }
}
