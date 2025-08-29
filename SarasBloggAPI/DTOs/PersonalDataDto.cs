namespace SarasBloggAPI.DTOs;

public sealed class PersonalDataDto
{
    public Dictionary<string, string?> Data { get; init; } = new();
    public List<string> Roles { get; init; } = new();
    public List<KeyValuePair<string, string>> Claims { get; init; } = new();
}

public sealed class DeleteMeRequestDto
{
    public string? Password { get; set; }
}
