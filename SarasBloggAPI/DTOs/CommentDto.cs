namespace SarasBloggAPI.DTOs
{
    public class CommentDto
    {
        public int Id { get; set; }
        public int BloggId { get; set; }
        public string Name { get; set; } = "";
        public string? Content { get; set; }
        public DateTime CreatedAt { get; set; }

        // nytt: rollinformation för färg
        public string? TopRole { get; set; }
    }
}
