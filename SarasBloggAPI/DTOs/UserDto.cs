namespace SarasBloggAPI.DTOs
{
    public class UserDto
    {
        public string Id { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? Name { get; set; }
        public string UserName { get; set; } = "";
        public IList<string> Roles { get; set; } = new List<string>();
    }
}
