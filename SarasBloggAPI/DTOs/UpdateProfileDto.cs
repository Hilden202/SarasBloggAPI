namespace SarasBloggAPI.DTOs
{
    public sealed class UpdateProfileDto
    {
        public string? PhoneNumber { get; set; }
        public string? Name { get; set; }
        public int? BirthYear { get; set; }
        public bool? NotifyOnNewPost { get; set; }
    }
}
