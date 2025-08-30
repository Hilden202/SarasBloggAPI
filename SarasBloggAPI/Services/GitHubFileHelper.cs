using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using System.Security.Cryptography;

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

        // ---------- SAVE (bloggId-mappar) ----------
        public async Task<string?> SaveImageAsync(IFormFile file, int bloggId, string folderName = "blogg")
        {
            if (file is null || file.Length == 0) return null;

            var fileName = GenerateFileName(file);
            var uploadPath = BuildUploadPath(_uploadFolder, folderName, bloggId.ToString(), fileName);

            var tempPath = Path.Combine(Path.GetTempPath(), fileName);
            try
            {
                using (var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                    await file.CopyToAsync(fs);

                var bytes = await File.ReadAllBytesAsync(tempPath);
                var body = new
                {
                    message = $"Upload {uploadPath} via SarasBlogg",
                    content = Convert.ToBase64String(bytes),
                    branch = _branch
                };

                var resp = await _httpClient.PutAsync(
                    $"https://api.github.com/repos/{_userName}/{_repository}/contents/{uploadPath}",
                    new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"));

                resp.EnsureSuccessStatusCode();

                return $"https://raw.githubusercontent.com/{_userName}/{_repository}/{_branch}/{uploadPath}";
            }
            finally
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }
        }

        // ---------- SAVE (bakåtkompatibel) ----------
        public async Task<string?> SaveImageAsync(IFormFile file, string folderName)
        {
            if (file is null || file.Length == 0) return null;

            var fileName = GenerateFileName(file);
            var uploadPath = BuildUploadPath(_uploadFolder, folderName, null, fileName);

            var tempPath = Path.Combine(Path.GetTempPath(), fileName);
            try
            {
                using (var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                    await file.CopyToAsync(fs);

                var bytes = await File.ReadAllBytesAsync(tempPath);
                var body = new
                {
                    message = $"Upload {uploadPath} via SarasBlogg",
                    content = Convert.ToBase64String(bytes),
                    branch = _branch
                };

                var resp = await _httpClient.PutAsync(
                    $"https://api.github.com/repos/{_userName}/{_repository}/contents/{uploadPath}",
                    new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"));

                resp.EnsureSuccessStatusCode();

                return $"https://raw.githubusercontent.com/{_userName}/{_repository}/{_branch}/{uploadPath}";
            }
            finally
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }
        }

        // ---------- DELETE (singel-fil) ----------
        public async Task DeleteImageAsync(string imageUrl, string folder)
        {
            if (string.IsNullOrWhiteSpace(imageUrl))
                return;

            string relativePath = null;

            // Försök hitta path via _uploadFolder som innan
            var marker = $"{_uploadFolder}/";
            var start = imageUrl.IndexOf(marker, StringComparison.Ordinal);
            if (start != -1)
            {
                relativePath = imageUrl.Substring(start);
            }
            else
            {
                // Fallback: Hantera gamla "raw.githubusercontent.com"-URL:er
                try
                {
                    var uri = new Uri(imageUrl);
                    var segments = uri.Segments
                        .Select(s => s.Trim('/'))
                        .Where(s => !string.IsNullOrEmpty(s))
                        .ToArray();

                    // Format: user / repo / branch / uploads / about / filename
                    var branchIndex = Array.IndexOf(segments, _branch);
                    if (branchIndex != -1 && branchIndex + 1 < segments.Length)
                    {
                        relativePath = string.Join('/', segments.Skip(branchIndex + 1));
                    }
                }
                catch
                {
                    return; // Ogiltig URL → gör inget
                }
            }

            if (string.IsNullOrEmpty(relativePath))
                return;

            // 🔹 Hämta SHA för filen
            var shaUrl = $"https://api.github.com/repos/{_userName}/{_repository}/contents/{relativePath}?ref={_branch}";
            var shaResponse = await _httpClient.GetAsync(shaUrl);
            if (!shaResponse.IsSuccessStatusCode) return;

            using var jsonDoc = JsonDocument.Parse(await shaResponse.Content.ReadAsStringAsync());
            if (!jsonDoc.RootElement.TryGetProperty("sha", out var shaProp)) return;

            var sha = shaProp.GetString();

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

            var deleteResponse = await _httpClient.SendAsync(deleteRequest);
            if (!deleteResponse.IsSuccessStatusCode)
            {
                Console.WriteLine($"[GitHub] Failed to delete image: {deleteResponse.StatusCode}");
            }
        }

        // ---------- DELETE (hela blogg-mappen) ----------
        public async Task DeleteBlogFolderAsync(int bloggId, string folderName = "blogg")
        {
            var path = BuildUploadPath(_uploadFolder, folderName, bloggId.ToString(), fileName: null);
            await DeleteDirectoryRecursiveAsync(path);
        }

        // ---------- helpers ----------
        private static string GenerateFileName(IFormFile file)
        {
            var original = Path.GetFileName(file.FileName);
            // millisekunder ger unik tidsstämpel även vid 4 snabba uploads
            var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd_HHmmssfff");
            // kryptorand eliminerar seed-krockar (ingen new Random())
            var random4 = RandomNumberGenerator.GetInt32(0, 10_000).ToString("D4");
            return $"{random4}-{timestamp}_{original}";
        }

        // robust mot "uploads/uploads"
        private static string BuildUploadPath(string root, string folder, string? bloggId, string? fileName)
        {
            var r = (root ?? "uploads").Trim().Trim('/', '\\'); // uploads
            var f = (folder ?? "").Trim().Trim('/', '\\');      // blogg / uploads/blogg
            if (f.StartsWith(r + "/", StringComparison.OrdinalIgnoreCase)) f = f[(r.Length + 1)..];
            if (string.Equals(f, r, StringComparison.OrdinalIgnoreCase)) f = "";

            var id = (bloggId ?? "").Trim().Trim('/', '\\');

            var parts = new List<string>();
            if (!string.IsNullOrEmpty(r)) parts.Add(r);
            if (!string.IsNullOrEmpty(f)) parts.Add(f);
            if (!string.IsNullOrEmpty(id)) parts.Add(id);
            if (!string.IsNullOrEmpty(fileName)) parts.Add(fileName);

            return string.Join('/', parts);
        }

        private async Task DeleteDirectoryRecursiveAsync(string path)
        {
            var listUrl = $"https://api.github.com/repos/{_userName}/{_repository}/contents/{path}?ref={_branch}";
            var resp = await _httpClient.GetAsync(listUrl);
            if (!resp.IsSuccessStatusCode) return;

            var json = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in doc.RootElement.EnumerateArray())
                {
                    var type = item.GetProperty("type").GetString(); // "file" | "dir"
                    var itemPath = item.GetProperty("path").GetString();
                    if (type == "file")
                    {
                        var sha = item.GetProperty("sha").GetString();
                        if (!string.IsNullOrEmpty(itemPath) && !string.IsNullOrEmpty(sha))
                            await DeleteByPathAndShaAsync(itemPath!, sha!);
                    }
                    else if (type == "dir")
                    {
                        await DeleteDirectoryRecursiveAsync(itemPath!);
                    }
                }
            }
            else if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                     doc.RootElement.TryGetProperty("type", out var t) &&
                     t.GetString() == "file")
            {
                var sha = doc.RootElement.GetProperty("sha").GetString();
                if (!string.IsNullOrEmpty(sha))
                    await DeleteByPathAndShaAsync(path, sha!);
            }
        }

        private async Task DeleteByPathAndShaAsync(string repoPath, string sha)
        {
            var body = new { message = $"Delete {repoPath} via SarasBlogg", sha, branch = _branch };
            var req = new HttpRequestMessage(HttpMethod.Delete,
                $"https://api.github.com/repos/{_userName}/{_repository}/contents/{repoPath}")
            {
                Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
            };
            await _httpClient.SendAsync(req);
        }

        private static string? ExtractRelativePath(string imageUrl)
        {
            if (string.IsNullOrWhiteSpace(imageUrl)) return null;

            if (imageUrl.Contains("raw.githubusercontent.com", StringComparison.OrdinalIgnoreCase))
            {
                var uri = new Uri(imageUrl);
                var segs = uri.Segments.Select(s => s.Trim('/')).ToArray();
                // ["", user, repo, branch, uploads, ...]
                if (segs.Length >= 5)
                {
                    // hoppa över user, repo, branch
                    return string.Join('/', segs.Skip(4));
                }
            }
            else if (imageUrl.Contains("/repos/", StringComparison.OrdinalIgnoreCase)
                  && imageUrl.Contains("/contents/", StringComparison.OrdinalIgnoreCase))
            {
                var marker = "/contents/";
                var idx = imageUrl.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
                if (idx != -1)
                {
                    var after = imageUrl[(idx + marker.Length)..];
                    var qIdx = after.IndexOf('?', StringComparison.Ordinal);
                    return qIdx >= 0 ? after[..qIdx] : after;
                }
            }
            return null;
        }

    }
}
