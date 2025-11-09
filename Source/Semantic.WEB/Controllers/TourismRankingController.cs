using Microsoft.AspNetCore.Mvc;
using Semantic.WEB.ApplicationLayer;

namespace Semantic.WEB.Controllers
{
    [ApiController]
    [Route("api/tourism")]
    public class TourismRankingController : ControllerBase
    {
        private readonly OpenDataService _openDataService;

        public TourismRankingController(OpenDataService openDataService)
        {
            _openDataService = openDataService;
        }

        [HttpGet("city-ranking")]
        public async Task<IActionResult> GetCityRanking([FromQuery] int limit = 100)
        {
            var results = await _openDataService.GetCityRankingAsync(limit);
            return Ok(results);
        }

        [HttpGet("city")]
        public async Task<IActionResult> GetCityByName([FromQuery] string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return BadRequest("City name is required.");
            }

            var city = await _openDataService.GetCityByNameAsync(name);
            if (city == null)
            {
                return NotFound($"No city found with name '{name}' in Ukraine.");
            }

            return Ok(city);
        }
    }
}
