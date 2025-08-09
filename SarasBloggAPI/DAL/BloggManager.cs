using Microsoft.EntityFrameworkCore;
using SarasBloggAPI.Data;
using SarasBloggAPI.Models;
using SarasBloggAPI.Services;

namespace SarasBloggAPI.DAL
{
    public class BloggManager
    {
        private readonly MyDbContext _context;
        private readonly BloggImageManager _imageManager;
        private readonly IFileHelper _fileHelper;
        private readonly ILogger<BloggManager> _logger;

        public BloggManager(MyDbContext context, BloggImageManager imageManager, IFileHelper fileHelper, ILogger<BloggManager> logger)
        {
            _context = context;
            _imageManager = imageManager;
            _fileHelper = fileHelper;
            _logger = logger;
        }

        public async Task<List<Blogg>> GetAllAsync()
        {
            return await _context.Bloggs.OrderByDescending(b => b.LaunchDate).ToListAsync();
        }

        public async Task<Blogg?> GetByIdAsync(int id)
        {
            return await _context.Bloggs.FirstOrDefaultAsync(b => b.Id == id);
        }

        public async Task<Blogg> CreateAsync(Blogg blogg)
        {
            _context.Bloggs.Add(blogg);
            await _context.SaveChangesAsync();
            return blogg;
        }

        public async Task<bool> UpdateAsync(Blogg blogg)
        {
            _context.Bloggs.Update(blogg);
            return await _context.SaveChangesAsync() > 0;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            // (1) Försök ta bort GitHub-mappen för bloggen
            try
            {
                await _fileHelper.DeleteBlogFolderAsync(id, "blogg");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Kunde inte radera GitHub-mapp för blogg {Id}", id);
                // vi fortsätter ändå så DB inte fastnar
            }

            // (2) Ta bort alla bildposter i DB
            await _imageManager.DeleteImagesByBloggIdAsync(id);

            // (3) Ta bort själva bloggen
            var blogg = await _context.Bloggs.FindAsync(id);
            if (blogg == null) return false;

            _context.Bloggs.Remove(blogg);
            return await _context.SaveChangesAsync() > 0;
        }
    }
}
