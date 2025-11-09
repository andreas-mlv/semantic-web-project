using System.Text.Json.Serialization;

namespace Semantic.WEB.Model
{
    public class CityDTO
    {
        [JsonPropertyName("cityUri")]
        public string CityUri { get; set; }

        [JsonPropertyName("cityLabel")]
        public string CityLabel { get; set; }

        [JsonPropertyName("cityEngName")]
        public string CityEngName { get; set; }

        [JsonPropertyName("totalVisitors")]
        public double TotalVisitors { get; set; }

        [JsonPropertyName("coordinates")]
        public string Coordinates { get; set; }

        [JsonPropertyName("logoUrl")]
        public string LogoUrl { get; set; }
    }
}
