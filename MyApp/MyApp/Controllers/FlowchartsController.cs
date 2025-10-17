using Microsoft.AspNetCore.Mvc;

namespace MyApp.Controllers
{
    public class FlowchartsController : Controller
    {
        [HttpGet]
        public IActionResult Index()
        {
            ViewData["Title"] = "Flowcharts";
            return View();
        }

        [HttpGet]
        public IActionResult Create()
        {
            ViewData["Title"] = "Crear flowchart";
            return View();
        }
    }
}
