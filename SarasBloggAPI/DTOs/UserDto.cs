namespace SarasBloggAPI.DTOs
{
    public class UserDto
    {
        public string Id { get; set; } = string.Empty;
        public string UserName { get; set; } = "";
        public string? Email { get; set; }
        public string? Name { get; set; }
        public int? BirthYear { get; set; }
        public bool EmailConfirmed { get; set; }
        public IList<string> Roles { get; set; } = new List<string>();
        public bool NotifyOnNewPost { get; set; }
    }

}
