using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SarasBloggAPI.Data;
using SarasBloggAPI.Models;

namespace SarasBloggAPI.Services
{
    public class NewPostNotifier
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<NewPostNotifier> _log;
        private readonly IConfiguration _cfg;

        public NewPostNotifier(IServiceScopeFactory scopeFactory,
                               ILogger<NewPostNotifier> log,
                               IConfiguration cfg)
        {
            _scopeFactory = scopeFactory;
            _log = log;
            _cfg = cfg;
        }

        public async Task NotifyAsync(int bloggId, CancellationToken ct = default)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MyDbContext>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var email = scope.ServiceProvider.GetRequiredService<IEmailSender>();

            _log.LogInformation("[Notify] NotifyAsync called for post {Id}", bloggId);

            var post = await db.Bloggs.AsNoTracking().FirstOrDefaultAsync(b => b.Id == bloggId, ct);
            if (post is null) { _log.LogWarning("Notify: post {Id} not found", bloggId); return; }
            if (post.Hidden || post.IsArchived) { _log.LogInformation("Notify: post {Id} not public -> skip", bloggId); return; }

            var frontendBase = _cfg["Frontend:BaseUrl"] ?? "https://sarasblogg.onrender.com";
            var postUrl = $"{frontendBase.TrimEnd('/')}/Blogg?showId={post.Id}";

            var subject = $"Nytt inlägg: {post.Title}";
            var html = $@"<p>Hej!</p>
                        <p>Ett nytt blogginlägg har publicerats: <strong>{System.Net.WebUtility.HtmlEncode(post.Title)}</strong></p>
                        <p><a href=""{postUrl}"">Läs inlägget</a></p>
                        <p>/SarasBlogg</p>";

            var recipients = await userManager.Users
                .Where(u => u.EmailConfirmed && u.NotifyOnNewPost && u.Email != null)
                .Select(u => u.Email!)
                .ToListAsync(ct);

            _log.LogInformation("[Notify] recipients={Count}", recipients.Count);

            foreach (var addr in recipients)
            {
                try { await email.SendAsync(addr, subject, html); }
                catch (Exception ex) { _log.LogError(ex, "Notify send failed to {Email}", addr); }
            }

            _log.LogInformation("Notify: sent to {Count} recipients for post {Id}", recipients.Count, bloggId);
        }
    }
}
