using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace Semantic.WEB
{
    [ApiController]
    [Route("api/tourism")]
    public class TourismRankingController : ControllerBase
    {
        private const string WikidataSparqlEndpoint = "https://query.wikidata.org/sparql";
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IMemoryCache _cache;

        private const string CacheKey = "TourismCityRankingCache";
        private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(6);

        public TourismRankingController(IHttpClientFactory httpClientFactory, IMemoryCache cache)
        {
            _httpClientFactory = httpClientFactory;
            _cache = cache;
        }

        /// <summary>
        /// Повертає список міст України, відсортованих за сумарною кількістю відвідувачів
        /// туристичних атракцій (на основі відкритих даних Wikidata).
        /// </summary>
        [HttpGet("city-ranking")]
        public async Task<IActionResult> GetCityRanking([FromQuery] int limit = 50)
        {
            if (_cache.TryGetValue(CacheKey + "_" + limit, out List<CityRankingDto> cached))
                return Ok(cached);

            string sparql = BuildSparqlQuery(limit);

            var client = _httpClientFactory.CreateClient("wikidata");
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/sparql-results+json"));

            var requestUri = WikidataSparqlEndpoint + "?query=" + Uri.EscapeDataString(sparql);

            using var resp = await client.GetAsync(requestUri);
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                return StatusCode((int)resp.StatusCode, new { error = "Помилка запиту до Wikidata", details = body });
            }

            var json = await resp.Content.ReadAsStringAsync();
            var results = ParseSparqlResults(json);

            _cache.Set(CacheKey + "_" + limit, results, CacheDuration);

            return Ok(results);
        }

        /// <summary>
        /// Формує SPARQL-запит для отримання міст України
        /// з сумарною кількістю відвідувачів туристичних атракцій.
        /// </summary>
        private string BuildSparqlQuery(int limit)
        {
            return $@"
PREFIX wd: <http://www.wikidata.org/entity/>
PREFIX wdt: <http://www.wikidata.org/prop/direct/>
PREFIX wikibase: <http://wikiba.se/ontology#>

SELECT ?city ?cityLabel (SUM(?visitors) AS ?totalVisitors) (SAMPLE(?cityCoord) AS ?coord) WHERE {{
  ?attraction (wdt:P31/wdt:P279*) wd:Q570116 .     # туристична атракція
  ?attraction wdt:P1174 ?visitors .                 # відвідувачів на рік
  ?attraction wdt:P131* ?city .                     # розташована в місті
  ?city (wdt:P31/wdt:P279*) wd:Q515 .               # місто
  ?city wdt:P17 wd:Q212 .                           # країна — Україна
  OPTIONAL {{ ?city wdt:P625 ?cityCoord. }}
  SERVICE wikibase:label {{ bd:serviceParam wikibase:language ""uk,en"" . }}
}}
GROUP BY ?city ?cityLabel
ORDER BY DESC(?totalVisitors)
LIMIT {limit}
";
        }

        /// <summary>
        /// Обробляє JSON-відповідь від Wikidata і перетворює її у список DTO.
        /// </summary>
        private List<CityRankingDto> ParseSparqlResults(string sparqlJson)
        {
            using var doc = JsonDocument.Parse(sparqlJson);
            var list = new List<CityRankingDto>();

            if (!doc.RootElement.TryGetProperty("results", out var results) ||
                !results.TryGetProperty("bindings", out var bindings))
                return list;

            foreach (var b in bindings.EnumerateArray())
            {
                var dto = new CityRankingDto();

                if (b.TryGetProperty("city", out var cityEl) && cityEl.TryGetProperty("value", out var cityVal))
                    dto.CityUri = cityVal.GetString();

                if (b.TryGetProperty("cityLabel", out var labelEl) && labelEl.TryGetProperty("value", out var labelVal))
                    dto.CityLabel = labelVal.GetString();

                if (b.TryGetProperty("totalVisitors", out var tv) && tv.TryGetProperty("value", out var tvv))
                {
                    if (double.TryParse(tvv.GetString(), out var dbl))
                        dto.TotalVisitors = dbl;
                }

                if (b.TryGetProperty("coord", out var coord) && coord.TryGetProperty("value", out var coordVal))
                    dto.Coordinates = coordVal.GetString();

                list.Add(dto);
            }

            return list;
        }
    }

    /// <summary>
    /// Модель даних для одного запису рейтингу міста.
    /// </summary>
    public class CityRankingDto
    {
        [JsonPropertyName("cityUri")]
        public string CityUri { get; set; }

        [JsonPropertyName("cityLabel")]
        public string CityLabel { get; set; }

        [JsonPropertyName("totalVisitors")]
        public double TotalVisitors { get; set; }

        [JsonPropertyName("coordinates")]
        public string Coordinates { get; set; }
    }
}
