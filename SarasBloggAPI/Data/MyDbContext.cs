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

        public DbSet<BloggImage> BloggImages { get; set; } = default!;
        public DbSet<BloggLike> BloggLikes => Set<BloggLike>();

        protected override void OnModelCreating(ModelBuilder b)
        {
            base.OnModelCreating(b);

            // Unik: en like per (Blogg, User)
            b.Entity<BloggLike>()
             .HasIndex(x => new { x.BloggId, x.UserId })
             .IsUnique();

            // === Cascade: User -> BloggLike ===
            b.Entity<BloggLike>()
             .HasOne<ApplicationUser>()      // ägare: användaren
             .WithMany()
             .HasForeignKey(x => x.UserId)
             .OnDelete(DeleteBehavior.Cascade);

            // === Cascade: Blogg -> BloggLike ===
            b.Entity<BloggLike>()
             .HasOne<Blogg>()                // ägare: blogginlägget
             .WithMany()
             .HasForeignKey(x => x.BloggId)
             .OnDelete(DeleteBehavior.Cascade);

            // === Cascade: User -> Comment ===
            b.Entity<Comment>()
             .HasOne<ApplicationUser>()
             .WithMany()
             .HasForeignKey(c => c.UserId)
             .OnDelete(DeleteBehavior.Cascade);

            // === Cascade: Blogg -> Comment ===
            b.Entity<Comment>()
             .HasOne<Blogg>()
             .WithMany()
             .HasForeignKey(c => c.BloggId)
             .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
