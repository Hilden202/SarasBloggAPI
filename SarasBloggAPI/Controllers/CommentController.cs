using Microsoft.AspNetCore.Mvc;

namespace SarasBloggAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CommentController : ControllerBase
    {
        [HttpGet] // Hämtar alla
        public List<Models.Comment> GetAllComments()
        {
            //// Simulera att vi hämtar kommentarer från en databas
            //if(DAL.CommentManager.GetComments().Count == 0)
            //{
            //    var comment = new Models.Comment
            //    {
            //        Id = 1,
            //        Name = "Patrik",
            //        Email = "putte@puttson.com",
            //        Content = "Hej hej",
            //        CreatedAt = DateTime.UtcNow,
            //        BloggId = 30
            //    };
            //    DAL.CommentManager.CreateComment(comment);
            //    // ta bort ovan när allt är på sin plats
            //}
            return DAL.CommentManager.GetComments();
        }

        [HttpGet("{id}")] // Hämta per id
        public Models.Comment GetComment(int id)
        {
            return DAL.CommentManager.GetComment(id);
        }

        [HttpPost]
        public void PostComment([FromBody] Models.Comment comment)
        {
            DAL.CommentManager.CreateComment(comment);
        }

        //[HttpPut("{id}")]
        //public void PutTransaction(int id, [FromBody] Models.Comment comment)
        //{
        //    DAL.CommentManager.UpdateComment(id, comment);
        //}

        [HttpDelete("{id}")]
        public void DeleteComment(int id)
        {
            DAL.CommentManager.DeleteComment(id);
        }

    }
}
