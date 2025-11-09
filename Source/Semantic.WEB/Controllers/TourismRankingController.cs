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
    }
}
