using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SarasBloggAPI.DAL;
using SarasBloggAPI.Services;

namespace SarasBloggAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CommentController : ControllerBase
    {
        private readonly DAL.CommentManager _commentManager;
        private readonly ContentSafetyService _contentSafetyService;

        public CommentController(CommentManager commentManager, ContentSafetyService contentSafetyService)
        {
            _commentManager = commentManager;
            _contentSafetyService = contentSafetyService;
        }

        [HttpGet] // Hämtar alla
        public async Task<List<Models.Comment>> GetAllCommentsAsync()
        {
            var comments = await _commentManager.GetCommentsAsync();
            return comments;
        }

        [HttpGet("ById/{id}")] // Hämta per id
        public async Task<Models.Comment> GetComment(int id)
        {
            var comment = await _commentManager.GetCommentAsync(id);
            return comment;
        }

        [HttpPost] // Skapa en kommentar
        public async Task<IActionResult> PostComment([FromBody] Models.Comment comment)
        {
            try
            {
                // Kolla att kommentaren inte är null eller tom
                if (comment == null || string.IsNullOrWhiteSpace(comment.Content))
                    return BadRequest("Kommentar kan inte vara tom.");

                // Analysera kommentaren med ContentSafetyService
                bool isSafe = await _contentSafetyService.IsContentSafeAsync(comment.Content);

                if (!isSafe)
                    return BadRequest("Kommentaren bedömdes som osäker och kan inte publiceras.");

                // Spara kommentaren via CommentManager
                await _commentManager.CreateCommentAsync(comment);

                return Ok(); // 200 OK
            }
            catch (Exception ex)
            {
                // Logga eventuellt ex.Message här om du har loggning
                return StatusCode(500, "Ett fel inträffade vid hantering av kommentaren.");
            }
        }


        [HttpDelete("ById/{id}")]
        public async Task DeleteComment(int id)
        {
            await _commentManager.DeleteComment(id);
        }

        [HttpDelete("ByBlogg/{bloggId}")]
        public async Task DeleteComments(int bloggId)
        {
            await _commentManager.DeleteComments(bloggId);
        }

        //[HttpPut("{id}")]
        //public async Task PutTransaction(int id, [FromBody] Models.Comment comment)
        //{
        //    await _commentManager.UpdateCommentAsync(id, comment);
        //}
    }
}
