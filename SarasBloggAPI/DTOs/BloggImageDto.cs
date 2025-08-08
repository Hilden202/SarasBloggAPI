namespace SarasBloggAPI.DTOs
{
    public class BloggImageDto
    {
        public int Id { get; set; }
        public int BloggId { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public int Order { get; set; }
    }
}