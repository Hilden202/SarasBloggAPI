using System.Security.Cryptography.X509Certificates;

namespace SarasBloggAPI.DAL
{
    public static class CommentManager
    {
        // databasemodell
        private static List<Models.Comment> Comments { get; set; } = new List<Models.Comment>();

        public static List<Models.Comment> GetComments()
        {
            return Comments;
        }

        public static Models.Comment GetComment(int id)
        {
            return Comments.Where(c => c.Id == id).SingleOrDefault();
        }

        public static void CreateComment(Models.Comment comment)
        {
            comment.Id = Comments.Count + 1;
            Comments.Add(comment);
        }

        //public static void UpdateComment(int id, Models.Comment comment)
        //{
        //    var existingComment = Comments.Where(c => c.Id == id).FirstOrDefault();
        //    if(existingComment != null)
        //    {
        //        existingComment.Name = comment.Name;
        //        existingComment.Email = comment.Email;
        //        existingComment.Content = comment.Content;
        //        existingComment.CreatedAt = comment.CreatedAt;
        //    }
        //}

        public static void DeleteComment(int id)
        {
            var existingComment = Comments.Where(c => c.Id == id).FirstOrDefault();
            Comments.Remove(existingComment);
        }
    }
}
