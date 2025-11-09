using Microsoft.Extensions.Caching.Memory;
using Semantic.WEB.Model;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Semantic.WEB.ApplicationLayer
{
    public class OpenDataService
    {
        private const string WikidataSparqlEndpoint = "https://query.wikidata.org/sparql";
        private const string WikipediaSummaryApi = "https://en.wikipedia.org/api/rest_v1/page/summary/";

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IMemoryCache _cache;

        private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(6);
        private const string CacheKey = "TourismCityRankingCache";

        private bool _initialise = false;

        public OpenDataService(IHttpClientFactory httpClientFactory, IMemoryCache cache)
        {
            _httpClientFactory = httpClientFactory;
            _cache = cache;
        }

        public async Task WarmUp()
        {
            if (_initialise)
            {
                return;
            }

            await GetCityRankingAsync(100);
        }

        public async Task<List<CityDTO>> GetCityRankingAsync(int limit)
        {
            string cacheKey = $"{CacheKey}_{limit}";
            if (_cache.TryGetValue(cacheKey, out List<CityDTO> cached))
            {
                return cached;
            }

            string sparql = BuildSparqlQuery(limit);
            var results = await ExecuteSparqlAsync(sparql);

            _cache.Set(cacheKey, results, CacheDuration);
            return results;
        }

        public async Task<CityDTO?> GetCityByNameAsync(string name)
        {
            string cacheKey = $"{CacheKey}_city_{name.ToLower()}";
            if (_cache.TryGetValue(cacheKey, out CityDTO cached))
            {
                return cached;
            }

            string sparql = BuildCityByNameQuery(name);
            var results = await ExecuteSparqlAsync(sparql);
            var city = results.FirstOrDefault();

            if (city != null)
            {
                _cache.Set(cacheKey, city, CacheDuration);
            }

            return city;
        }

        private async Task<List<CityDTO>> ExecuteSparqlAsync(string sparql)
        {
            var client = _httpClientFactory.CreateClient("wikidata");
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/sparql-results+json"));

            var requestUri = WikidataSparqlEndpoint + "?query=" + Uri.EscapeDataString(sparql);
            using var resp = await client.GetAsync(requestUri);

            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                throw new InvalidOperationException($"SPARQL query failed: {body}");
            }

            var json = await resp.Content.ReadAsStringAsync();
            return await ParseSparqlResults(json);
        }

        private string BuildSparqlQuery(int limit)
        {
            return $@"
PREFIX wd: <http://www.wikidata.org/entity/>
PREFIX wdt: <http://www.wikidata.org/prop/direct/>
PREFIX wikibase: <http://wikiba.se/ontology#>

SELECT ?city ?cityLabel (SAMPLE(?enLabel) AS ?enCityLabel) (SUM(?visitors) AS ?totalVisitors)
       (COUNT(?attraction) AS ?attractionCount) (SAMPLE(?cityCoord) AS ?coord) (SAMPLE(?logo) AS ?logo)
WHERE {{
  ?city (wdt:P31/wdt:P279*) wd:Q515.
  ?city wdt:P17 wd:Q212.
  OPTIONAL {{ ?city wdt:P154 ?logo. }}
  OPTIONAL {{ ?city rdfs:label ?enLabel. FILTER(LANG(?enLabel) = 'en') }}
  ?attraction (wdt:P31/wdt:P279*) wd:Q570116.
  ?attraction wdt:P131* ?city.
  OPTIONAL {{ ?attraction wdt:P1174 ?visitors. }}
  OPTIONAL {{ ?city wdt:P625 ?cityCoord. }}
  SERVICE wikibase:label {{ bd:serviceParam wikibase:language ""uk,en"". }}
}}
GROUP BY ?city ?cityLabel
ORDER BY DESC(?totalVisitors)
LIMIT {limit}";
        }

        private string BuildCityByNameQuery(string name)
        {
            return $@"
PREFIX wd: <http://www.wikidata.org/entity/>
PREFIX wdt: <http://www.wikidata.org/prop/direct/>
PREFIX wikibase: <http://wikiba.se/ontology#>
PREFIX rdfs: <http://www.w3.org/2000/01/rdf-schema#>

SELECT ?city ?cityLabel (SAMPLE(?enLabel) AS ?enCityLabel) (SAMPLE(?cityCoord) AS ?coord)
       (SAMPLE(?logo) AS ?logo) (SUM(?visitors) AS ?totalVisitors) (COUNT(?attraction) AS ?attractionCount)
WHERE {{
  ?city (wdt:P31/wdt:P279*) wd:Q515.
  ?city wdt:P17 wd:Q212.
  ?city rdfs:label ""{name}""@en.
  OPTIONAL {{ ?city wdt:P154 ?logo. }}
  OPTIONAL {{ ?city rdfs:label ?enLabel. FILTER(LANG(?enLabel) = 'en') }}
  ?attraction (wdt:P31/wdt:P279*) wd:Q570116.
  ?attraction wdt:P131* ?city.
  OPTIONAL {{ ?attraction wdt:P1174 ?visitors. }}
  OPTIONAL {{ ?city wdt:P625 ?cityCoord. }}
  SERVICE wikibase:label {{ bd:serviceParam wikibase:language ""uk,en"". }}
}}
GROUP BY ?city ?cityLabel
LIMIT 1";
        }

        private async Task<List<CityDTO>> ParseSparqlResults(string sparqlJson)
        {
            using var doc = JsonDocument.Parse(sparqlJson);
            var list = new List<CityDTO>();

            if (!doc.RootElement.TryGetProperty("results", out var results) ||
                !results.TryGetProperty("bindings", out var bindings))
            {
                return list;
            }

            foreach (var b in bindings.EnumerateArray())
            {
                var dto = new CityDTO();

                if (b.TryGetProperty("city", out var cityEl) && cityEl.TryGetProperty("value", out var cityVal))
                {
                    dto.CityUri = cityVal.GetString();
                }

                if (b.TryGetProperty("cityLabel", out var labelEl) && labelEl.TryGetProperty("value", out var labelVal))
                {
                    dto.CityLabel = labelVal.GetString();
                }

                if (b.TryGetProperty("enCityLabel", out var enLabelEl) && enLabelEl.TryGetProperty("value", out var enLabelVal))
                {
                    dto.CityEngName = enLabelVal.GetString();
                }

                if (b.TryGetProperty("attractionCount", out var tv) && tv.TryGetProperty("value", out var tvv))
                {
                    if (double.TryParse(tvv.GetString(), System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out var dbl))
                    {
                        dto.TotalVisitors = dbl * 1000;
                    }
                }

                if (b.TryGetProperty("coord", out var coord) && coord.TryGetProperty("value", out var coordVal))
                {
                    dto.Coordinates = coordVal.GetString();
                }

                if (b.TryGetProperty("logo", out var logo) && logo.TryGetProperty("value", out var logoVal))
                {
                    dto.LogoUrl = logoVal.GetString();
                }

                if (string.IsNullOrEmpty(dto.LogoUrl) && !string.IsNullOrEmpty(dto.CityEngName))
                {
                    var wikiImage = await FetchWikipediaImageAsync(dto.CityEngName);
                    if (!string.IsNullOrEmpty(wikiImage))
                    {
                        dto.LogoUrl = wikiImage;
                    }
                }

                list.Add(dto);
            }

            return list;
        }

        private async Task<string?> FetchWikipediaImageAsync(string englishName)
        {
            try
            {
                var client = _httpClientFactory.CreateClient("wikipedia");
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(
                    new MediaTypeWithQualityHeaderValue("application/json"));

                client.DefaultRequestHeaders.UserAgent.Clear();
                client.DefaultRequestHeaders.UserAgent.ParseAdd(
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) " +
                    "AppleWebKit/537.36 (KHTML, like Gecko) " +
                    "Chrome/129.0.0.0 Safari/537.36");

                var url = WikipediaSummaryApi + Uri.EscapeDataString(englishName);
                using var resp = await client.GetAsync(url);

                if (!resp.IsSuccessStatusCode)
                {
                    return null;
                }

                var json = await resp.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);

                if (doc.RootElement.TryGetProperty("originalimage", out var img) &&
                    img.TryGetProperty("source", out var src))
                {
                    return src.GetString();
                }
            }
            catch
            {
                // ignore
            }

            return null;
        }
    }
}
