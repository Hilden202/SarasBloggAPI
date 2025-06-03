using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SarasBloggAPI.DAL;

namespace SarasBloggAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CommentController : ControllerBase
    {
        private readonly DAL.CommentsManager _commentsManager;

        public CommentController(CommentsManager commentsManager)
        {
            _commentsManager = commentsManager;
        }

        [HttpGet] // Hämtar alla
        public async Task<List<Models.Comment>> GetAllCommentsAsync()
        {
            var comments = await _commentsManager.GetCommentsAsync();
            return comments;
        }

        [HttpGet("ById/{id}")] // Hämta per id
        public async Task<Models.Comment> GetComment(int id)
        {
            var comment = await _commentsManager.GetCommentAsync(id);
            return comment;
        }

        [HttpPost]
        public async Task PostComment([FromBody] Models.Comment comment)
        {
            await _commentsManager.CreateCommentAsync(comment);
        }


        [HttpDelete("ById/{id}")]
        public async Task DeleteComment(int id)
        {
            await _commentsManager.DeleteComment(id);
        }

        [HttpDelete("ByBlogg/{bloggId}")]
        public async Task DeleteComments(int bloggId)
        {
            await _commentsManager.DeleteComments(bloggId);
        }

        //[HttpPut("{id}")]
        //public async Task PutTransaction(int id, [FromBody] Models.Comment comment)
        //{
        //    await _commentsManager.UpdateCommentAsync(id, comment);
        //}
    }
}
