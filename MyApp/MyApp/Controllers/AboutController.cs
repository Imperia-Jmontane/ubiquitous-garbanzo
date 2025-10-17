using Microsoft.AspNetCore.Mvc;

namespace MyApp.Controllers
{
    public class AboutController : Controller
    {
        [HttpGet]
        public IActionResult Index()
        {
            ViewData["Title"] = "About";
            return View();
        }
    }
}
