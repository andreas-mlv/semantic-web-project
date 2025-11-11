using Microsoft.Extensions.Caching.Memory;
using Semantic.WEB.ApplicationLayer;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Semantic.WEB.Tests
{
    public class OpenDataServiceTests
    {
        private readonly IMemoryCache _cache;

        public OpenDataServiceTests()
        {
            _cache = new MemoryCache(new MemoryCacheOptions());
        }

        private OpenDattaService CreateService(string jsonResponse, string? wikipediaResponse = null)
        {
            var handler = new MockHttpHandler(jsonResponse, wikipediaResponse);
            var factory = new TestHttpClientFactory(handler);
            return new OpenDattaService();
        }

        [Fact]
        public async Task GetCityRankingAsync_ShouldReturnParsedCities()
        {
            string json = BuildWikidataJson("Київ", "Kyiv", "https://upload.wikimedia.org/wikipedia/commons/9/9e/Logo_of_Kyiv%2C_Ukraine_%28English%29.svg", 5000000, 34);
            var service = CreateService(json);

            var result = await service.GetCityRankingAsync(5);

            Assert.Single(result);
            Assert.Equal("Київ", result[0].CityLabel);
            Assert.Equal("Kyiv", result[0].CityEngName);
            Assert.Equal(5000000, result[0].TotalVisitors);
            Assert.Equal("https://upload.wikimedia.org/wikipedia/commons/9/9e/Logo_of_Kyiv%2C_Ukraine_%28English%29.svg", result[0].LogoUrl);
        }

        [Fact]
        public async Task GetCityRankingAsync_ShouldCacheResults()
        {
            string json = BuildWikidataJson("Київ", "Kyiv", "https://upload.wikimedia.org/wikipedia/commons/9/9e/Logo_of_Kyiv%2C_Ukraine_%28English%29.svg", 5000000, 34);
            var service = CreateService(json);

            var result1 = await service.GetCityRankingAsync(5);
            var result2 = await service.GetCityRankingAsync(5);

            // Assert: same object reference means cached
            Assert.Equal(result1.GetHashCode(), result2.GetHashCode());
            Assert.Same(result1, result2);
        }


        [Fact]
        public async Task ParseSparqlResults_ShouldFallbackToWikipedia_WhenNoLogo()
        {
            string wikidataJson = BuildWikidataJson("Київ", "Kyiv", null, 1000000, 12);
            string wikiJson = JsonSerializer.Serialize(new
            {
                originalimage = new { source = "https://upload.wikimedia.org/wikipedia/commons/9/9e/Logo_of_Kyiv%2C_Ukraine_%28English%29.svg" }
            });
            var service = CreateService(wikidataJson, wikiJson);

            var result = await service.GetCityRankingAsync(50);

            Assert.Single(result);
            Assert.Equal("https://upload.wikimedia.org/wikipedia/commons/9/9e/Logo_of_Kyiv%2C_Ukraine_%28English%29.svg", result[0].LogoUrl);
        }

        // ---------- Helpers ----------

        private static string BuildWikidataJson(string cityLabel, string cityEng, string? logo, double visitors, int attractionCount)
        {
            var data = new
            {
                results = new
                {
                    bindings = new[]
                    {
                        new Dictionary<string, object?>
                        {
                            ["city"] = new { value = "http://www.wikidata.org/entity/Q1" },
                            ["cityLabel"] = new { value = cityLabel },
                            ["enCityLabel"] = new { value = cityEng },
                            ["totalVisitors"] = new { value = visitors.ToString() },
                            ["attractionCount"] = new { value = attractionCount.ToString() },
                            ["coord"] = new { value = "50.4501,30.5234" },
                            ["logo"] = logo != null ? new { value = logo } : null
                        }
                    }
                }
            };

            return JsonSerializer.Serialize(data, new JsonSerializerOptions { IgnoreNullValues = true });
        }

        private class MockHttpHandler : HttpMessageHandler
        {
            private readonly string _wikidataResponse;
            private readonly string? _wikipediaResponse;

            public MockHttpHandler(string wikidataResponse, string? wikipediaResponse)
            {
                _wikidataResponse = wikidataResponse;
                _wikipediaResponse = wikipediaResponse;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var uri = request.RequestUri!.ToString();
                string content = uri.Contains("wikipedia.org")
                    ? (_wikipediaResponse ?? "{}")
                    : _wikidataResponse;

                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(content, Encoding.UTF8, "application/json")
                };
                return Task.FromResult(response);
            }
        }

        private class TestHttpClientFactory : IHttpClientFactory
        {
            private readonly HttpMessageHandler _handler;

            public TestHttpClientFactory(HttpMessageHandler handler)
            {
                _handler = handler;
            }

            public HttpClient CreateClient(string name)
            {
                return new HttpClient(_handler, disposeHandler: false);
            }
        }
    }
}
