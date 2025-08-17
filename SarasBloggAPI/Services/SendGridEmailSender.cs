using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace SarasBloggAPI.Services
{
    public sealed class SendGridEmailSender : IEmailSender
    {
        private readonly IHttpClientFactory _httpFactory;
        private readonly ILogger<SendGridEmailSender> _log;
        private readonly IConfiguration _cfg;

        public SendGridEmailSender(IHttpClientFactory httpFactory, ILogger<SendGridEmailSender> log, IConfiguration cfg)
        {
            _httpFactory = httpFactory;
            _log = log;
            _cfg = cfg;
        }

        public async Task SendAsync(string to, string subject, string htmlBody)
        {
            var apiKey = _cfg["Email:SendGridApiKey"] ?? throw new InvalidOperationException("Missing Email:SendGridApiKey");
            var fromEmail = _cfg["Email:FromEmail"] ?? "no-reply@sarasblogg.se";
            var fromName = _cfg["Email:FromName"] ?? "SarasBlogg";
            var replyTo = _cfg["Email:ReplyToEmail"]; // valfritt

            // enkel plaintext-version (fallback för klienter)
            var plain = StripHtmlToPlainText(htmlBody);

            var payload = new
            {
                personalizations = new[] { new { to = new[] { new { email = to } } } },
                from = new { email = fromEmail, name = fromName },
                reply_to = string.IsNullOrWhiteSpace(replyTo) ? null : new { email = replyTo },
                subject = subject, // ex: "Bekräfta din e-post till SarasBlogg"
                content = new[]
                {
            new { type = "text/plain", value = plain },
            new { type = "text/html",  value = htmlBody }
        }
            };

            var http = _httpFactory.CreateClient("sendgrid");
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            var resp = await http.PostAsync("https://api.sendgrid.com/v3/mail/send",
                new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));

            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                _log.LogError("SendGrid failed: {Status} {Body}", resp.StatusCode, body);
                throw new InvalidOperationException("Email send failed");
            }
        }

        private static string StripHtmlToPlainText(string html)
        {
            if (string.IsNullOrWhiteSpace(html)) return "";
            // superenkel strip: ta bort taggar + decode basic entities
            var text = System.Text.RegularExpressions.Regex.Replace(html, "<.*?>", string.Empty);
            return System.Net.WebUtility.HtmlDecode(text);
        }

    }
}
