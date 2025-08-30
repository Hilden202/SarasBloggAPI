namespace SarasBloggAPI.Services
{
    public class GitHubUploadOptions
    {
        public string Token { get; set; } = "";
        public string Owner { get; set; } = "";       // mappas från "UserName" i Program.cs
        public string Repo { get; set; } = "";        // mappas från "Repository" i Program.cs
        public string Branch { get; set; } = "main";
        public string UploadFolder { get; set; } = "uploads";
    }
}
