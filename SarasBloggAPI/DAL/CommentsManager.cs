using Microsoft.EntityFrameworkCore;

namespace SarasBloggAPI.DAL
{
    public class CommentsManager
    {
        private readonly Models.MyDbContext _context;

        public CommentsManager(Models.MyDbContext context)
        {
            _context = context;
        }

        public async Task<List<Models.Comment>> GetCommentsAsync()
        {
            return await _context.Comments.ToListAsync();
        }

        public async Task<Models.Comment?> GetCommentAsync(int id)
        {
            return await _context.Comments.FirstOrDefaultAsync(c => c.Id == id);
        }

        public async Task CreateCommentAsync(Models.Comment comment)
        {
            await _context.Comments.AddAsync(comment);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteComment(int id)
        {
            var existingComment = await _context.Comments.FirstOrDefaultAsync(c => c.Id == id);
            if (existingComment != null)
            {
                _context.Comments.Remove(existingComment);
                await _context.SaveChangesAsync();
            }
        }

        public async Task DeleteComments(int bloggId)
        {
            var existingComments = await _context.Comments.Where(c => c.BloggId == bloggId).ToListAsync();
            if (existingComments.Any())
            {
                _context.Comments.RemoveRange(existingComments);
                await _context.SaveChangesAsync();
            }
        }

        //public async Task UpdateCommentAsync(int id, Models.Comment comment)
        //{
        //    var existingComment = await _context.Comments.FirstOrDefaultAsync(c => c.Id == id);
        //    if (existingComment != null)
        //    {
        //        existingComment.Name = comment.Name;
        //        existingComment.Email = comment.Email;
        //        existingComment.Content = comment.Content;
        //        existingComment.CreatedAt = comment.CreatedAt;
        //        await _context.SaveChangesAsync();
        //    }
        //}


    }
}
