using Microsoft.EntityFrameworkCore;

namespace SarasBloggAPI.Models
{
    public class MyDbContext : DbContext
    {
        public MyDbContext(DbContextOptions<MyDbContext> options) : base(options)
        {

        }

        public DbSet<Comment> Comments { get; set; }
    }
}
