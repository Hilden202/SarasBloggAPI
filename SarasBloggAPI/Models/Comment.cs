using Microsoft.AspNetCore.Mvc.Formatters;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace SarasBloggAPI.Models
{
    public class Comment
    {
        public int? Id { get; set; }
        public string Name { get; set; }
        public string? Email { get; set; }
        public string Content { get; set; }
        public DateTime CreatedAt { get; set; }
        public int BloggId { get; set; }

        // NYTT: koppling till inloggad användare
        public string? UserId { get; set; }

        public Comment()
        {
            Name = string.Empty;
            Content = string.Empty;
            CreatedAt = DateTime.UtcNow;
        }

    }
}
