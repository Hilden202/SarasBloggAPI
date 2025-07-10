using Microsoft.EntityFrameworkCore;
using SarasBloggAPI.Data;
using SarasBloggAPI.Models;

namespace SarasBloggAPI.DAL
{
    public class ForbiddenWordManager
    {
        private readonly MyDbContext _context;

        public ForbiddenWordManager(MyDbContext context)
        {
            _context = context;
        }

        public async Task<List<ForbiddenWord>> GetAllAsync()
        {
            return await _context.ForbiddenWords.ToListAsync();
        }

        public async Task<ForbiddenWord?> GetByIdAsync(int id)
        {
            return await _context.ForbiddenWords.FindAsync(id);
        }

        public async Task<ForbiddenWord> CreateAsync(ForbiddenWord word)
        {
            _context.ForbiddenWords.Add(word);
            await _context.SaveChangesAsync();
            return word;
        }

        public async Task<bool> UpdateAsync(ForbiddenWord word)
        {
            var existing = await _context.ForbiddenWords.FindAsync(word.Id);
            if (existing == null) return false;

            existing.WordPattern = word.WordPattern;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var word = await _context.ForbiddenWords.FindAsync(id);
            if (word == null) return false;

            _context.ForbiddenWords.Remove(word);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
