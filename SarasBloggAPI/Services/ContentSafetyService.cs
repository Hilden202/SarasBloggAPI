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
                    //SEXUALLY_EXPLICIT = new { }, /fungerade inte då jag la till sv så valde att ta utesluta denna.
                    THREAT = new { },
                    IDENTITY_ATTACK = new { },
                    INSULT = new { }
                },
                languages = new[] { "en", "sv" }
            };

            var json = JsonSerializer.Serialize(requestBody);
            var requestContent = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            var response = await _client.PostAsync(url, requestContent);

            if (!response.IsSuccessStatusCode)
            {
                // För tidigare felsökning.
                //var errorContent = await response.Content.ReadAsStringAsync();
                //System.Diagnostics.Debug.WriteLine("ERROR FROM GOOGLE:");
                //System.Diagnostics.Debug.WriteLine(errorContent); // Visa varför det blev 400
                return false;
            }

            var jsonString = await response.Content.ReadAsStringAsync();

            var result = JsonSerializer.Deserialize<PerspectiveApiResponse>(jsonString);

            const double threshold = 0.7; // finns skala 0.5 stoppar allt, 0.7 medel och 0.9 släpper igenom nästan allt

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
