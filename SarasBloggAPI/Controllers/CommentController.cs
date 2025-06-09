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
                bool isNameSafe = await _contentSafetyService.IsContentSafeAsync(comment.Name);
                bool isContentSafe = await _contentSafetyService.IsContentSafeAsync(comment.Content);

                if (!isNameSafe)
                    return BadRequest("Namnet innehåller otillåtet språk.");

                if (!isContentSafe)
                    return BadRequest("Kommentaren bedömdes som osäker och kan inte publiceras.");

                await _commentManager.CreateCommentAsync(comment);

                return Ok(); // 200 OK
            }
            catch (Exception ex)
            {
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
