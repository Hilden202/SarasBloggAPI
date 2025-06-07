using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SarasBloggAPI.Models.Ai
{
    public class PerspectiveApiResponse
    {
        [JsonPropertyName("attributeScores")]
        public Dictionary<string, AttributeScore> AttributeScores { get; set; }

        public class AttributeScore
        {
            [JsonPropertyName("summaryScore")]
            public SummaryScore SummaryScore { get; set; }
        }

        public class SummaryScore
        {
            [JsonPropertyName("value")]
            public double Value { get; set; }

            [JsonPropertyName("type")]
            public string Type { get; set; }
        }
    }
}
