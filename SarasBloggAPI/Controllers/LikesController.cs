// Controllers/LikesController.cs
using Microsoft.AspNetCore.Mvc;
using SarasBloggAPI.Data;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using SarasBloggAPI.DTOs;
using SarasBloggAPI.Models;

[ApiController]
[Route("api/[controller]")]
public class LikesController : ControllerBase
{
    private readonly MyDbContext _db;
    public LikesController(MyDbContext db) => _db = db;

    private string? MyUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier);

    // Hämta count + om användaren gillat
    [AllowAnonymous]
    [HttpGet("{bloggId}/{userId?}")]
    public async Task<ActionResult<LikeDto>> GetCount(int bloggId, string? userId = null)
    {
        // 1) Hämta total count
        var count = await _db.BloggLikes.CountAsync(x => x.BloggId == bloggId);

        // 2) Räkna ut om nuvarande (eller given) användare har gillat
        var liked = false;
        var effectiveUserId = MyUserId() ?? userId; // claims vinner, annars route-param för bakåtkomp
        if (!string.IsNullOrEmpty(effectiveUserId))
        {
            liked = await _db.BloggLikes
                .AnyAsync(x => x.BloggId == bloggId && x.UserId == effectiveUserId);
        }

        // 3) Returnera DTO
        return Ok(new LikeDto
        {
            BloggId = bloggId,
            Count = count,
            UserId = effectiveUserId ?? "",
            Liked = liked
        });
    }

    // Lägg till gilla (idempotent)
    [Authorize(Policy = "RequireUser")]
    [HttpPost]
    public async Task<ActionResult<LikeDto>> Add([FromBody] LikeDto dto)
    {
        var myId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(myId)) return Unauthorized();

        var exists = await _db.BloggLikes
            .AnyAsync(x => x.BloggId == dto.BloggId && x.UserId == myId);

        if (!exists)
        {
            _db.BloggLikes.Add(new BloggLike { BloggId = dto.BloggId, UserId = myId });
            await _db.SaveChangesAsync();
        }

        var count = await _db.BloggLikes.CountAsync(x => x.BloggId == dto.BloggId);
        return Ok(new LikeDto { BloggId = dto.BloggId, UserId = myId, Count = count });
    }

    [Authorize(Policy = "RequireUser")]
    [HttpDelete("{bloggId}/{userId}")]
    public async Task<IActionResult> Unlike(int bloggId, string userId)
    {
        var myId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(myId)) return Unauthorized();

        var like = await _db.BloggLikes
            .FirstOrDefaultAsync(l => l.BloggId == bloggId && l.UserId == myId);

        if (like == null) return NotFound(new { message = "Like not found" });

        _db.BloggLikes.Remove(like);
        await _db.SaveChangesAsync();

        var count = await _db.BloggLikes.CountAsync(l => l.BloggId == bloggId);
        return Ok(new LikeDto { BloggId = bloggId, UserId = myId, Count = count });
    }

}
