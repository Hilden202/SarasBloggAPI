using Microsoft.EntityFrameworkCore;
using SarasBloggAPI.Data;
using SarasBloggAPI.Models;

namespace SarasBloggAPI.DAL
{
    public class AboutMeManager
    {
        private readonly MyDbContext _context;

        public AboutMeManager(MyDbContext context)
        {
            _context = context;
        }

        public async Task<AboutMe?> GetAsync()
        {
            return await _context.AboutMe.FirstOrDefaultAsync();
        }

        public async Task<AboutMe> CreateAsync(AboutMe aboutMe)
        {
            _context.AboutMe.Add(aboutMe);
            await _context.SaveChangesAsync();
            return aboutMe;
        }

        public async Task<bool> UpdateAsync(AboutMe aboutMe)
        {
            var existing = await _context.AboutMe.FindAsync(aboutMe.Id);
            if (existing == null) return false;

            existing.Title = aboutMe.Title;
            existing.Content = aboutMe.Content;
            existing.Image = aboutMe.Image;
            existing.UserId = aboutMe.UserId;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var entry = await _context.AboutMe.FindAsync(id);
            if (entry == null) return false;

            _context.AboutMe.Remove(entry);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
