// Controllers/LikesController.cs
using Microsoft.AspNetCore.Mvc;
using SarasBloggAPI.Data;
using Microsoft.EntityFrameworkCore;
using System.Linq;

[ApiController]
[Route("api/[controller]")]
public class LikesController : ControllerBase
{
    private readonly MyDbContext _db;
    public LikesController(MyDbContext db) => _db = db;

    // Hämta count + om användaren gillat
    [HttpGet("{bloggId}/{userId?}")]
    public async Task<ActionResult<LikeDto>> GetCount(int bloggId, string? userId = null)
    {
        var count = await _db.BloggLikes.CountAsync(x => x.BloggId == bloggId);
        var liked = false;

        if (!string.IsNullOrEmpty(userId))
        {
            liked = await _db.BloggLikes
                .AnyAsync(x => x.BloggId == bloggId && x.UserId == userId);
        }

        return Ok(new LikeDto { BloggId = bloggId, Count = count, UserId = userId ?? "", Liked = liked });
    }

    // Lägg till gilla (idempotent)
    [HttpPost]
    public async Task<ActionResult<LikeDto>> Add([FromBody] LikeDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.UserId))
            return BadRequest("UserId required.");

        var exists = await _db.BloggLikes
            .AnyAsync(x => x.BloggId == dto.BloggId && x.UserId == dto.UserId);

        if (!exists)
        {
            _db.BloggLikes.Add(new BloggLike { BloggId = dto.BloggId, UserId = dto.UserId });
            await _db.SaveChangesAsync();
        }

        var count = await _db.BloggLikes.CountAsync(x => x.BloggId == dto.BloggId);
        return Ok(new LikeDto { BloggId = dto.BloggId, UserId = dto.UserId, Count = count, Liked = true });
    }

    [HttpDelete("{bloggId}/{userId}")]
    public async Task<IActionResult> Unlike(int bloggId, string userId)
    {
        var like = await _db.BloggLikes
            .FirstOrDefaultAsync(l => l.BloggId == bloggId && l.UserId == userId);

        if (like == null)
            return NotFound(new { message = "Like not found" });

        _db.BloggLikes.Remove(like);
        await _db.SaveChangesAsync();

        var count = await _db.BloggLikes.CountAsync(l => l.BloggId == bloggId);
        return Ok(new LikeDto { BloggId = bloggId, UserId = userId, Count = count, Liked = false });
    }
}
