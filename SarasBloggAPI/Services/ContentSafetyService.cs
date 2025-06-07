using System.Net.Http.Json;
using System.Text.Json;
using SarasBloggAPI.Models.Ai;

namespace SarasBloggAPI.Services
{
    public class ContentSafetyService
    {
        private readonly string _apiKey;
        private readonly HttpClient _client;

        public ContentSafetyService(IConfiguration configuration, HttpClient client)
        {
            _apiKey = configuration["PerspectiveApi:ApiKey"];
            _client = client;
        }

        public async Task<bool> IsContentSafeAsync(string content)
        {
            var url = $"https://commentanalyzer.googleapis.com/v1alpha1/comments:analyze?key={_apiKey}";

            var requestBody = new
            {
                comment = new { text = content },
                requestedAttributes = new
                {
                    TOXICITY = new { },
                    SEXUALLY_EXPLICIT = new { },
                    THREAT = new { },
                    IDENTITY_ATTACK = new { },
                    INSULT = new { }
                },
                languages = new[] { "en" }
            };

            var json = JsonSerializer.Serialize(requestBody);
            var requestContent = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            var response = await _client.PostAsync(url, requestContent);

            if (!response.IsSuccessStatusCode)
            {
                // Log or handle error here  
                return false;
            }

            var jsonString = await response.Content.ReadAsStringAsync();

            // We can create a custom model for the response matching Perspective API  
            var result = JsonSerializer.Deserialize<PerspectiveApiResponse>(jsonString);

            // Interpret scores - we can set a threshold value, e.g., 0.7  
            const double threshold = 0.7;

            bool isSafe = true;
            if (result?.AttributeScores != null)
            {
                foreach (var attr in result.AttributeScores)
                {
                    if (attr.Value.SummaryScore.Value >= threshold)
                    {
                        isSafe = false;
                        break;
                    }
                }
            }

            return isSafe;
        }
    }
}
