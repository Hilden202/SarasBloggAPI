// Data/Entities/BloggLike.cs
public class BloggLike
{
    public int Id { get; set; }
    public int BloggId { get; set; }
    public string UserId { get; set; } = "";   // tillfälligt: username eller email
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
