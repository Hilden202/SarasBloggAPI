using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SarasBloggAPI.DAL;
using SarasBloggAPI.Data;
using SarasBloggAPI.Services;
using SarasBloggAPI.DTOs;
using Microsoft.EntityFrameworkCore;

namespace SarasBloggAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CommentController : ControllerBase
    {
        private readonly CommentManager _commentManager;
        private readonly ContentSafetyService _contentSafetyService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly MyDbContext _db;

        public CommentController(
            CommentManager commentManager,
            ContentSafetyService contentSafetyService,
            UserManager<ApplicationUser> userManager,
            MyDbContext db)
        {
            _commentManager = commentManager;
            _contentSafetyService = contentSafetyService;
            _userManager = userManager;
            _db = db;
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

        private async Task<CommentDto> ToDtoAsync(Models.Comment c)
        {
            var name = c.Name;
            string? topRole = null;

            if (!string.IsNullOrWhiteSpace(c.Email))
            {
                try
                {
                    var (user, top) = await ResolveBestUserByEmailAsync(c.Email);
                    if (user != null)
                    {
                        name = user.UserName ?? name;
                        topRole = top;
                    }
                }
                catch
                {
                    // ignore lookup errors – render the comment anyway
                }
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

        private async Task<(ApplicationUser? User, string? TopRole)> ResolveBestUserByEmailAsync(string email)
        {
            // Get all users that share this email (handles the duplicate-email case gracefully)
            var candidates = await _userManager.Users
                .Where(u => u.Email != null && u.Email == email)
                .ToListAsync();

            ApplicationUser? best = null;
            string? bestTopRole = null;
            var bestRank = int.MaxValue;

            foreach (var u in candidates)
            {
                var roles = await _userManager.GetRolesAsync(u);
                var top = GetTopRole(roles) ?? "";
                var rank = RoleRank.TryGetValue(top, out var r) ? r : 999;

                if (rank < bestRank)
                {
                    best = u;
                    bestTopRole = top;
                    bestRank = rank;
                }
            }

            return (best, bestTopRole);
        }

        private async Task<List<string>> GetForbiddenWordsAsync()
        {
            return await _db.ForbiddenWords
                .Select(f => f.WordPattern)
                .ToListAsync();
        }


        // ===== Endpoints =====

        // Alla (även utloggade) ska se färger/TopRole
        [AllowAnonymous]
        [HttpGet]
        public async Task<List<CommentDto>> GetAllCommentsAsync()
        {
            try
            {
                var comments = await _commentManager.GetCommentsAsync();
                var list = new List<CommentDto>(comments.Count);
                foreach (var c in comments)
                    list.Add(await ToDtoAsync(c));
                // Censurera här innan return
                var forbidden = await GetForbiddenWordsAsync();
                foreach (var dto in list)
                {
                    dto.Content = TextCensorshipHelper.CensorForbiddenPatterns(dto.Content, forbidden);
                    dto.Name = TextCensorshipHelper.CensorForbiddenPatterns(dto.Name, forbidden);
                }
                return list;
            }
            catch
            {
                return new List<CommentDto>();
            }
        }

        [AllowAnonymous]
        [HttpGet("by-blogg/{bloggId:int}")]
        public async Task<List<CommentDto>> GetByBloggAsync(int bloggId)
        {
            try
            {
                var all = await _commentManager.GetCommentsAsync();
                var filtered = all.Where(c => c.BloggId == bloggId)
                                  .OrderBy(c => c.CreatedAt)
                                  .ToList();

                var list = new List<CommentDto>(filtered.Count);
                foreach (var c in filtered)
                    list.Add(await ToDtoAsync(c));

                // Censurerar även här innan return (samma som i GetAll & GetComment)
                var forbidden = await GetForbiddenWordsAsync();
                foreach (var dto in list)
                {
                    dto.Content = TextCensorshipHelper.CensorForbiddenPatterns(dto.Content, forbidden);
                    dto.Name = TextCensorshipHelper.CensorForbiddenPatterns(dto.Name, forbidden);
                }
                return list;
            }
            catch
            {
                return new List<CommentDto>();
            }
        }


        // Hämta en kommentar
        [AllowAnonymous]
        [HttpGet("ById/{id:int}")]
        public async Task<ActionResult<CommentDto>> GetComment(int id)
        {
            var c = await _commentManager.GetCommentAsync(id);
            if (c == null) return NotFound();
            var dto = await ToDtoAsync(c);
            var forbidden = await GetForbiddenWordsAsync();
            dto.Content = TextCensorshipHelper.CensorForbiddenPatterns(dto.Content, forbidden);
            dto.Name = TextCensorshipHelper.CensorForbiddenPatterns(dto.Name, forbidden);
            return dto;
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
                    // NYTT: koppla UserId (för cascade delete)
                    comment.UserId = myId;

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
                    comment.UserId = null;  // viktigt: lämna null för anonyma
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
            var isOwner = false;

            // Primärt: jämför UserId (nya kommentarer)
            if (!string.IsNullOrWhiteSpace(existing.UserId) &&
                string.Equals(existing.UserId, me?.Id, StringComparison.Ordinal))
            {
                isOwner = true;
            }

            // Fallback: gammal modell som bara använde Email
            if (!isOwner && !string.IsNullOrWhiteSpace(existing.Email) &&
                string.Equals(existing.Email, me?.Email, StringComparison.OrdinalIgnoreCase))
            {
                isOwner = true;
            }


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
