using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using SarasBloggAPI.Data;
using SarasBloggAPI.Models;
using SarasBloggAPI.Services;

namespace SarasBloggAPI.Services
{
    public class NewPostNotifier
    {
        private readonly MyDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmailSender _email;
        private readonly ILogger<NewPostNotifier> _log;
        private readonly IConfiguration _cfg;

        public NewPostNotifier(
            MyDbContext db,
            UserManager<ApplicationUser> userManager,
            IEmailSender email,
            ILogger<NewPostNotifier> log,
            IConfiguration cfg)
        {
            _db = db;
            _userManager = userManager;
            _email = email;
            _log = log;
            _cfg = cfg;
        }

        public async Task NotifyAsync(int bloggId, CancellationToken ct = default)
        {
            var post = await _db.Bloggs.AsNoTracking()
                .FirstOrDefaultAsync(b => b.Id == bloggId, ct);

            if (post is null)
            {
                _log.LogWarning("Notify: post {Id} not found", bloggId);
                return;
            }
            if (post.Hidden || post.IsArchived)
            {
                _log.LogInformation("Notify: post {Id} not public -> skip", bloggId);
                return;
            }

            var frontendBase = _cfg["Frontend:BaseUrl"] ?? "https://sarasblogg.onrender.com";
            var postUrl = $"{frontendBase}/Blogg/Details/{post.Id}";

            var subject = $"Nytt inlägg: {post.Title}";
            var html = $@"<p>Hej!</p>
<p>Ett nytt blogginlägg har publicerats: <strong>{System.Net.WebUtility.HtmlEncode(post.Title)}</strong></p>
<p><a href=""{postUrl}"">Läs inlägget</a></p>
<p>/SarasBlogg</p>";

            var recipients = await _userManager.Users
                .Where(u => u.EmailConfirmed && u.NotifyOnNewPost && u.Email != null)
                .Select(u => u.Email!)
                .ToListAsync(ct);

            foreach (var email in recipients)
            {
                try
                {
                    await _email.SendAsync(email, subject, html);
                }
                catch (Exception ex)
                {
                    _log.LogError(ex, "Notify send failed to {Email}", email);
                }
            }

            _log.LogInformation("Notify: sent to {Count} recipients for post {Id}", recipients.Count, bloggId);
        }
    }
}
