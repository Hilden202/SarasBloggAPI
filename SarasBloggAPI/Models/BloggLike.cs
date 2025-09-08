using System;

namespace SarasBloggAPI.Models
{
    public class BloggLike
    {
        public int Id { get; set; }
        public int BloggId { get; set; }
        public string UserId { get; set; } = "";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
