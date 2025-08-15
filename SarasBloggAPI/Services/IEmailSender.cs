using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace SarasBloggAPI.Services
{
    public interface IEmailSender
    {
        Task SendAsync(string to, string subject, string htmlBody);
    }

    public sealed class DevEmailSender : IEmailSender
    {
        private readonly ILogger<DevEmailSender> _log;
        public DevEmailSender(ILogger<DevEmailSender> log) => _log = log;

        public Task SendAsync(string to, string subject, string htmlBody)
        {
            _log.LogInformation("DEV-MAIL to {To}: {Subject}\n{Body}", to, subject, htmlBody);
            return Task.CompletedTask;
        }
    }
}
