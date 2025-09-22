using Microsoft.AspNetCore.Identity;

namespace SarasBloggAPI.Data
{
    public class ApplicationUser : IdentityUser
    {
        public int? BirthYear { get; set; }
        public string? Name { get; set; }
        public string? ProfileImageUrl { get; set; }
        public bool NotifyOnNewPost { get; set; } = false;
    }
}
