namespace SarasBloggAPI.DTOs;

public sealed class PersonalDataDto
{
    public Dictionary<string, string?> Data { get; init; } = new();
    public List<string> Roles { get; init; } = new();
    public List<KeyValuePair<string, string>> Claims { get; init; } = new();
    public int CommentsCount { get; init; }
    public int LikesCount { get; init; }
    public List<CommentPreviewDto>? Comments { get; init; }
    public List<LikePreviewDto>? Likes { get; init; }
}

public sealed class DeleteMeRequestDto
{
    public string? Password { get; set; }
}

public sealed class CommentPreviewDto
{
    public int Id { get; set; }
    public int BloggId { get; set; }
    public string BloggTitle { get; set; } = "";
    public string Content { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

public sealed class LikePreviewDto
{
    public int Id { get; set; }
    public int BloggId { get; set; }
    public string BloggTitle { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}
