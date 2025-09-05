using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using SarasBloggAPI.Data;

namespace SarasBloggAPI.DAL
{
    public class CommentManager
    {
        private readonly MyDbContext _context;

        public CommentManager(MyDbContext context)
        {
            _context = context;
        }

        public async Task<List<Models.Comment>> GetCommentsAsync()
        {
            var list = await _context.Comments.AsNoTracking().ToListAsync();

            // Hämta aktuella usernames för alla unika e-postadresser i listan
            var emails = list.Where(c => !string.IsNullOrWhiteSpace(c.Email))
                                     .Select(c => c.Email!)
                                     .Distinct(StringComparer.OrdinalIgnoreCase)
                                     .ToList();

            if (emails.Count > 0)
            {
                var map = await _context.Users
                    .Where(u => u.Email != null && emails.Contains(u.Email))
                    .GroupBy(u => u.Email!)
                    .Select(g => new
                    {
                        Email = g.Key,
                        // choose a single username per email; tweak the ordering as you like
                        UserName = g
                            .OrderByDescending(x => x.EmailConfirmed)
                            .Select(x => x.UserName)
                            .FirstOrDefault()
                    })
                    .ToDictionaryAsync(x => x.Email, x => x.UserName ?? "", StringComparer.OrdinalIgnoreCase);


                foreach (var c in list)
                {
                    if (!string.IsNullOrWhiteSpace(c.Email) &&
                         map.TryGetValue(c.Email!, out var currentName) &&
                         !string.IsNullOrWhiteSpace(currentName))
                    {
                        c.Name = currentName; // alltid aktuellt namn för medlemmar
                    }
                }
            }

            return list;
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
