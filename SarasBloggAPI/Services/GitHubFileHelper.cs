using System.Net.Http.Headers;
using System.Text.Json;
using System.Text;

namespace SarasBloggAPI.Services
{
    public class GitHubFileHelper : IFileHelper
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _config;

        private readonly string _token;
        private readonly string _userName;
        private readonly string _repository;
        private readonly string _branch;
        private readonly string _uploadFolder;

        public GitHubFileHelper(IConfiguration config)
        {
            _config = config;
            _httpClient = new HttpClient();

            _token = _config["GitHubUpload:Token"];
            _userName = _config["GitHubUpload:UserName"];
            _repository = _config["GitHubUpload:Repository"];
            _branch = _config["GitHubUpload:Branch"];
            _uploadFolder = _config["GitHubUpload:UploadFolder"];

            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _token);
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("SarasBloggApp");
        }

        public async Task<string> SaveImageAsync(IFormFile file, string folderName)
        {
            var originalName = Path.GetFileName(file.FileName);
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd");
            var random = new Random().Next(0, 10000).ToString("D4"); // 0000–9999
            var fileName = $"{random}-{timestamp}_{originalName}";

            var tempPath = Path.Combine(Path.GetTempPath(), fileName);

            try
            {
                using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await file.CopyToAsync(stream);
                }

                var uploadPath = $"{folderName}/{fileName}";
                var bytes = await File.ReadAllBytesAsync(tempPath);

                var uploadRequest = new
                {
                    message = $"Ladda upp bild: {fileName}",
                    content = Convert.ToBase64String(bytes),
                    branch = _branch
                };

                var json = JsonSerializer.Serialize(uploadRequest);
                var httpContent = new StringContent(json, Encoding.UTF8, "application/json");

                var putUrl = $"https://api.github.com/repos/{_userName}/{_repository}/contents/{uploadPath}";
                var response = await _httpClient.PutAsync(putUrl, httpContent);
                response.EnsureSuccessStatusCode();

                // Returnera raw-URL mot vald branch
                return $"https://raw.githubusercontent.com/{_userName}/{_repository}/{_branch}/{uploadPath}";
            }
            finally
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }

        public async Task DeleteImageAsync(string imageUrl, string folder)
        {
            if (string.IsNullOrWhiteSpace(imageUrl))
                return;

            // Hitta relativ sökväg (börja vid uploads/ eller vad _uploadFolder är)
            var marker = $"{_uploadFolder}/";
            var start = imageUrl.IndexOf(marker, StringComparison.Ordinal);
            if (start == -1)
                return;

            var relativePath = imageUrl.Substring(start); // t.ex. uploads/1234-20250807_bild.jpg

            // 1) Hämta SHA för filen på given branch
            var shaUrl = $"https://api.github.com/repos/{_userName}/{_repository}/contents/{relativePath}?ref={_branch}";
            var shaResponse = await _httpClient.GetAsync(shaUrl);
            if (!shaResponse.IsSuccessStatusCode)
                return;

            var jsonStr = await shaResponse.Content.ReadAsStringAsync();
            using var jsonDoc = JsonDocument.Parse(jsonStr);
            if (!jsonDoc.RootElement.TryGetProperty("sha", out var shaProp))
                return;

            var sha = shaProp.GetString();
            if (string.IsNullOrEmpty(sha))
                return;

            // 2) Skicka DELETE med commit-meddelande + sha + branch
            var body = new
            {
                message = $"Delete {relativePath} via SarasBlogg",
                sha = sha,
                branch = _branch
            };

            var json = JsonSerializer.Serialize(body);
            var deleteContent = new StringContent(json, Encoding.UTF8, "application/json");

            var deleteUrl = $"https://api.github.com/repos/{_userName}/{_repository}/contents/{relativePath}";
            var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, deleteUrl)
            {
                Content = deleteContent
            };

            await _httpClient.SendAsync(deleteRequest);
        }
    }
}