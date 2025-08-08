using SarasBloggAPI.Data;
using SarasBloggAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace SarasBloggAPI.DAL
{
    public class BloggImageManager
    {
        private readonly MyDbContext _context;

        public BloggImageManager(MyDbContext context)
        {
            _context = context;
        }

        public async Task<List<BloggImage>> GetImagesByBloggIdAsync(int bloggId)
        {
            return await _context.BloggImages
                .Where(img => img.BloggId == bloggId)
                .OrderBy(img => img.Order)
                .ToListAsync();
        }

        public async Task<BloggImage?> GetImageByIdAsync(int id)
        {
            return await _context.BloggImages.FindAsync(id);
        }

        public async Task AddImageAsync(BloggImage image)
        {
            // Sätt Order: högsta + 1 inom samma blogg
            var maxOrder = await _context.BloggImages
                .Where(i => i.BloggId == image.BloggId)
                .MaxAsync(i => (int?)i.Order) ?? -1;

            image.Order = maxOrder + 1;

            _context.BloggImages.Add(image);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteImageAsync(int id)
        {
            var image = await _context.BloggImages.FindAsync(id);
            if (image == null) return;

            _context.BloggImages.Remove(image);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteImagesByBloggIdAsync(int bloggId)
        {
            var images = await _context.BloggImages
                .Where(i => i.BloggId == bloggId)
                .ToListAsync();

            if (images.Count == 0) return;

            _context.BloggImages.RemoveRange(images);
            await _context.SaveChangesAsync();
        }
    }
}