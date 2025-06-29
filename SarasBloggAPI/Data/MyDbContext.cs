using Microsoft.EntityFrameworkCore;
using SarasBloggAPI.Models;

namespace SarasBloggAPI.Data
{
    public class MyDbContext : DbContext
    {
        public MyDbContext(DbContextOptions<MyDbContext> options) : base(options)
        {

        }

        public DbSet<Blogg> Bloggs { get; set; }

        public DbSet<Comment> Comments { get; set; }
    }
}
