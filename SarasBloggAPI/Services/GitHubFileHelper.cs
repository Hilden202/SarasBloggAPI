using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Linq;
using SarasBloggAPI.Models;

namespace SarasBloggAPI.Services
{
    /// <summary>
    /// Lagrar/raderar filer i ett GitHub-repo via Contents API på ett minnessnålt sätt:
    /// - Base64-kodar bilddata i ström
    /// - Skriver hela GitHub-JSON:en till en tempfil (ingen stor sträng i RAM)
    /// - Skickar JSON med StreamContent och per-försök-ny request (retry-safe)
    /// </summary>
    public sealed class GitHubFileHelper : IFileHelper
    {
        private readonly HttpClient _http;
        private readonly ILogger<GitHubFileHelper>? _logger;
        private readonly string _owner;
        private readonly string _repo;
        private readonly string _branch;
        private readonly string _token;
        private readonly string _rootFolder = "uploads";
        private readonly string? _mediaEnv; // "test" | "prod" | null (ingen extra nivå)

        public GitHubFileHelper(HttpClient http, IConfiguration cfg, ILogger<GitHubFileHelper>? logger = null)
        {
            _http = http ?? throw new ArgumentNullException(nameof(http));
            _logger = logger;

            // Stöd både "GitHub:*" och "GitHubUpload:*" (back-compat)
            _owner = cfg["GitHub:Owner"] ?? cfg["GitHubUpload:UserName"]
                      ?? throw new InvalidOperationException("GitHub Owner/UserName saknas.");

            _repo = cfg["GitHub:Repo"] ?? cfg["GitHubUpload:Repository"]
                      ?? throw new InvalidOperationException("GitHub Repo/Repository saknas.");

            _branch = cfg["GitHub:Branch"] ?? cfg["GitHubUpload:Branch"] ?? "main";
            _token = cfg["GitHub:Token"] ?? cfg["GitHubUpload:Token"]
                      ?? throw new InvalidOperationException("GitHub Token saknas.");

            _http.DefaultRequestHeaders.UserAgent.ParseAdd("SarasBlogg/1.0");
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token);
            _http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            _http.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");

            // (valfritt) konfigurerbar uploads-rot:
            _rootFolder = cfg["GitHub:UploadFolder"] ?? cfg["GitHubUpload:UploadFolder"] ?? "uploads";

            // Miljösegment för att separera media mellan test/prod (ex: "test" i Development, "prod" i appsettings.json)
            _mediaEnv = cfg["GitHub:MediaEnv"]; // null/tom => inget extra segment
        }

        // --- IFileHelper ---

        // Bakåt-compat (utan bloggId): lägg i uploads/{env?}/{folderName}/
        public async Task<string?> SaveImageAsync(IFormFile file, string folderName)
        {
            if (file == null) throw new ArgumentNullException(nameof(file));

            var ext = SafeExtension(file.FileName);
            var fileName = $"{Guid.NewGuid():N}{ext}";
            var repoPath = JoinPath(_rootFolder, _mediaEnv, folderName ?? "misc", fileName);

            return await PutFileToGitHubAsync(file, repoPath);
        }

        // Primär: uploads/{env?}/{folderName}/{bloggId}/
        public async Task<string?> SaveImageAsync(IFormFile file, int bloggId, string folderName = "blogg")
        {
            if (file == null) throw new ArgumentNullException(nameof(file));

            var ext = SafeExtension(file.FileName);
            var fileName = $"{Guid.NewGuid():N}{ext}";
            var repoPath = JoinPath(_rootFolder, _mediaEnv, folderName ?? "blogg", bloggId.ToString(), fileName);

            return await PutFileToGitHubAsync(file, repoPath);
        }

        public async Task DeleteImageAsync(string imageUrl, string folder)
        {
            // Stöd både raw-url och repo-relativ path
            var repoPath = TryExtractRepoPathFromRawUrl(imageUrl) ?? imageUrl.TrimStart('/');
            if (string.IsNullOrWhiteSpace(repoPath))
            {
                _logger?.LogWarning("DeleteImageAsync: tomt repoPath för url '{Url}'", imageUrl);
                return;
            }

            var sha = await GetContentShaAsync(repoPath);
            if (sha == null)
            {
                _logger?.LogInformation("DeleteImageAsync: {Path} saknar sha (kanske redan raderad).", repoPath);
                return;
            }

            var deleteUrl = BuildContentsUrl(repoPath);
            var payload = new { message = "Delete blog image", branch = _branch, sha };
            var payloadBytes = JsonSerializer.SerializeToUtf8Bytes(payload); // liten payload → ok i minnet

            using var resp = await SendWithRetryAsync(() =>
            {
                var content = new ByteArrayContent(payloadBytes);
                content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                return new HttpRequestMessage(HttpMethod.Delete, deleteUrl) { Content = content };
            });

            resp.EnsureSuccessStatusCode();
        }

        public async Task DeleteBlogFolderAsync(int bloggId, string folderName = "blogg")
        {
            // Endast nuvarande env-sökväg
            var current = JoinPath(_rootFolder, _mediaEnv, folderName ?? "blogg", bloggId.ToString());
            await DeleteFolderRecursiveAsync(current);
        }

        // --- Upload helper ---

        private async Task<string?> PutFileToGitHubAsync(IFormFile file, string repoPath)
        {
            var putUrl = BuildContentsUrl(repoPath);

            // Skriv GitHub-JSON till TEMP-fil medan vi strömmar Base64 (minimerar RAM)
            var tmpJsonPath = Path.GetTempFileName();

            try
            {
                await using (var fs = new FileStream(tmpJsonPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096))
                await using (var writer = new StreamWriter(fs, new UTF8Encoding(false)))
                {
                    // JSON-head
                    await writer.WriteAsync("{\"message\":\"Add blog image\",\"branch\":\"");
                    await writer.WriteAsync(_branch);
                    await writer.WriteAsync("\",\"content\":\"");
                    await writer.FlushAsync(); // flush innan vi skriver råa base64-bytes

                    // Base64-transform
                    await using var inStream = file.OpenReadStream();
                    using var b64 = new ToBase64Transform();

                    var inBuf = ArrayPool<byte>.Shared.Rent(64 * 1024);
                    var outBuf = ArrayPool<byte>.Shared.Rent(((64 * 1024) / 3 + 2) * 4); // safe

                    var tail = new byte[2];
                    var tailLen = 0;

                    try
                    {
                        while (true)
                        {
                            var read = await inStream.ReadAsync(inBuf, 0, inBuf.Length);
                            if (read == 0) break;

                            var total = tailLen + read;
                            var span = new byte[total];
                            Buffer.BlockCopy(tail, 0, span, 0, tailLen);
                            Buffer.BlockCopy(inBuf, 0, span, tailLen, read);

                            var fullLen = total - (total % b64.InputBlockSize);
                            var outLen = b64.TransformBlock(span, 0, fullLen, outBuf, 0);
                            await fs.WriteAsync(outBuf.AsMemory(0, outLen));

                            tailLen = total - fullLen;
                            if (tailLen > 0)
                                Buffer.BlockCopy(span, fullLen, tail, 0, tailLen);
                        }

                        var final = b64.TransformFinalBlock(tail, 0, tailLen);
                        if (final.Length > 0)
                            await fs.WriteAsync(final, 0, final.Length);
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(inBuf);
                        ArrayPool<byte>.Shared.Return(outBuf);
                    }

                    // JSON-tail
                    await writer.WriteAsync("\"}");
                }

                // Skicka PUT med streamad JSON; bygg NY request per försök (retry-safe)
                using var resp = await SendWithRetryAsync(() =>
                {
                    var jsonStream = new FileStream(tmpJsonPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    var content = new StreamContent(jsonStream);
                    content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                    return new HttpRequestMessage(HttpMethod.Put, putUrl) { Content = content };
                });

                resp.EnsureSuccessStatusCode();
                return BuildRawUrl(repoPath);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Uppladdning misslyckades för {RepoPath}", repoPath);
                throw;
            }
            finally
            {
                try { System.IO.File.Delete(tmpJsonPath); } catch { /* ignore */ }
            }
        }

        // --- GitHub helpers ---

        private string SafeExtension(string fileName)
        {
            var ext = Path.GetExtension(fileName);
            if (string.IsNullOrWhiteSpace(ext)) return ".bin";
            return ext;
        }

        private string BuildContentsUrl(string repoPath)
        {
            var pathEscaped = Uri.EscapeDataString(repoPath).Replace("%2F", "/");
            return $"https://api.github.com/repos/{_owner}/{_repo}/contents/{pathEscaped}";
        }

        private string BuildRawUrl(string repoPath)
        {
            var segs = repoPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < segs.Length; i++) segs[i] = Uri.EscapeDataString(segs[i]);
            return $"https://raw.githubusercontent.com/{_owner}/{_repo}/{_branch}/{string.Join('/', segs)}";
        }

        private string? TryExtractRepoPathFromRawUrl(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return null;
            if (!"raw.githubusercontent.com".Equals(uri.Host, StringComparison.OrdinalIgnoreCase)) return null;

            // /{owner}/{repo}/{branch}/{path...}
            var segs = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segs.Length < 4) return null;
            if (!string.Equals(segs[0], _owner, StringComparison.OrdinalIgnoreCase)) return null;
            if (!string.Equals(segs[1], _repo, StringComparison.OrdinalIgnoreCase)) return null;

            var pathSegs = new List<string>();
            for (int i = 3; i < segs.Length; i++)
                pathSegs.Add(Uri.UnescapeDataString(segs[i]));
            return string.Join('/', pathSegs);
        }

        private async Task<string?> GetContentShaAsync(string repoPath)
        {
            var url = BuildContentsUrl(repoPath) + $"?ref={Uri.EscapeDataString(_branch)}";
            using var resp = await _http.GetAsync(url);
            if (resp.StatusCode == HttpStatusCode.NotFound) return null;
            resp.EnsureSuccessStatusCode();

            await using var s = await resp.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(s);

            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty("sha", out var shaProp))
            {
                return shaProp.GetString();
            }
            return null;
        }

        private async Task DeleteFolderRecursiveAsync(string repoPath)
        {
            var listUrl = BuildContentsUrl(repoPath) + $"?ref={Uri.EscapeDataString(_branch)}";
            using var resp = await _http.GetAsync(listUrl);
            if (resp.StatusCode == HttpStatusCode.NotFound) return;
            resp.EnsureSuccessStatusCode();

            await using var s = await resp.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(s);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return;

            var dirs = new List<string>();

            foreach (var item in doc.RootElement.EnumerateArray())
            {
                var type = item.GetProperty("type").GetString();
                var path = item.GetProperty("path").GetString();
                if (string.IsNullOrEmpty(type) || string.IsNullOrEmpty(path)) continue;

                if (type == "file")
                {
                    var sha = item.GetProperty("sha").GetString();
                    var deleteUrl = BuildContentsUrl(path);
                    var payload = new { message = "Delete blog image", branch = _branch, sha };

                    using var respDel = await SendWithRetryAsync(() =>
                    {
                        var json = JsonSerializer.Serialize(payload); // liten payload
                        var content = new StringContent(json, Encoding.UTF8, "application/json");
                        return new HttpRequestMessage(HttpMethod.Delete, deleteUrl) { Content = content };
                    });
                    respDel.EnsureSuccessStatusCode();
                }
                else if (type == "dir")
                {
                    dirs.Add(path);
                }
            }

            foreach (var dir in dirs)
                await DeleteFolderRecursiveAsync(dir);
        }

        /// <summary>
        /// Skickar request med exponentiell backoff och stöd för Retry-After.
        /// Bygger en NY HttpRequestMessage per försök så att streamar kan återskapas.
        /// </summary>
        private async Task<HttpResponseMessage> SendWithRetryAsync(Func<HttpRequestMessage> requestFactory, int maxAttempts = 5)
        {
            for (var attempt = 1; ; attempt++)
            {
                using var req = requestFactory();
                using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);

                // 2xx–4xx (förutom 429) -> returnera
                if ((int)resp.StatusCode < 500 && resp.StatusCode != (HttpStatusCode)429)
                    return CloneResponse(resp);

                if (attempt >= maxAttempts)
                    return CloneResponse(resp);

                var delay = TimeSpan.FromMilliseconds(200 * Math.Pow(2, attempt - 1));
                if (resp.Headers.RetryAfter?.Delta is TimeSpan ra && ra > TimeSpan.Zero)
                    delay = ra;

                _logger?.LogWarning("GitHub: {Status}. Försök {Attempt}/{Max}. Väntar {Delay}.",
                    (int)resp.StatusCode, attempt, maxAttempts, delay);

                await Task.Delay(delay);
            }
        }

        // Klona svaret eftersom vi disponerar originalet i using
        private static HttpResponseMessage CloneResponse(HttpResponseMessage resp)
        {
            var clone = new HttpResponseMessage(resp.StatusCode)
            {
                ReasonPhrase = resp.ReasonPhrase,
                Version = resp.Version,
                RequestMessage = resp.RequestMessage
            };

            foreach (var h in resp.Headers)
                clone.Headers.TryAddWithoutValidation(h.Key, h.Value);

            if (resp.Content != null)
            {
                var bytes = resp.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
                clone.Content = new ByteArrayContent(bytes);
                foreach (var h in resp.Content.Headers)
                    clone.Content.Headers.TryAddWithoutValidation(h.Key, h.Value);
            }

            return clone;
        }

        // Bygg en path där tomma/null-delar filtreras bort
        private static string JoinPath(params string?[] parts)
            => string.Join('/', parts.Where(p => !string.IsNullOrWhiteSpace(p)));
    }
}
