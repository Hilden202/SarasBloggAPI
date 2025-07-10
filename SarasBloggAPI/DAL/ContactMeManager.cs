using Microsoft.EntityFrameworkCore;
using SarasBloggAPI.Data;
using SarasBloggAPI.Models;

namespace SarasBloggAPI.DAL
{
    public class ContactMeManager
    {
        private readonly MyDbContext _context;

        public ContactMeManager(MyDbContext context)
        {
            _context = context;
        }

        public async Task<List<ContactMe>> GetAllAsync()
        {
            return await _context.ContactMe.ToListAsync();
        }

        public async Task<ContactMe> CreateAsync(ContactMe contact)
        {
            _context.ContactMe.Add(contact);
            await _context.SaveChangesAsync();
            return contact;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var item = await _context.ContactMe.FindAsync(id);
            if (item == null) return false;

            _context.ContactMe.Remove(item);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
