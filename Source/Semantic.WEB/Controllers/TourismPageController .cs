using Microsoft.AspNetCore.Mvc;
using Semantic.WEB.ApplicationLayer;

namespace Semantic.WEB.Controllers
{
    public class TourismPageController : Controller
    {
        private readonly OpenDataService _openDataService;

        public TourismPageController(OpenDataService openDataService)
        {
            _openDataService = openDataService;
        }

        [HttpGet("/")]
        public IActionResult Root()
        {
            return Redirect("/city/0");
        }

        [HttpGet("/city/{page?}")]
        public async Task<IActionResult> Index(int page = 1)
        {
            var data = await _openDataService.GetCityRankingAsync(100);
            ViewData["Page"] = page;
            return View("Index", data);
        }
    }

}
