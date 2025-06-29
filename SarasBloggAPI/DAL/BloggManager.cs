using Microsoft.EntityFrameworkCore;
using SarasBloggAPI.Data;
using SarasBloggAPI.Models;

namespace SarasBloggAPI.DAL
{
    public class BloggManager
    {
        private readonly MyDbContext _context;

        public BloggManager(MyDbContext context)
        {
            _context = context;
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
            var blogg = await _context.Bloggs.FindAsync(id);
            if (blogg == null) return false;
            _context.Bloggs.Remove(blogg);
            return await _context.SaveChangesAsync() > 0;
        }
    }
}
