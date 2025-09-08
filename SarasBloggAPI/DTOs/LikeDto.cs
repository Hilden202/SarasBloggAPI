namespace SarasBloggAPI.DTOs
{
    public class LikeDto
    {
        public int BloggId { get; set; }
        public string UserId { get; set; } = "";
        public int Count { get; set; }
        public bool Liked { get; set; }
    }
}