using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SarasBloggAPI.Models;

namespace SarasBloggAPI.Data
{
    public class MyDbContext : IdentityDbContext<ApplicationUser>

    {
        public MyDbContext(DbContextOptions<MyDbContext> options) : base(options)
        {

        }

        public DbSet<Blogg> Bloggs { get; set; }

        public DbSet<Comment> Comments { get; set; }

        public DbSet<ForbiddenWord> ForbiddenWords { get; set; }

        public DbSet<AboutMe> AboutMe { get; set; } = default!;

        public DbSet<ContactMe> ContactMe { get; set; } = default!;

    }
}
