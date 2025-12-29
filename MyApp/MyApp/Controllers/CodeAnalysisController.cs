using Microsoft.AspNetCore.Mvc;

namespace MyApp.Controllers
{
    public sealed class CodeAnalysisController : Controller
    {
        [HttpGet]
        public IActionResult Index(string? repositoryId)
        {
            string resolvedRepositoryId = repositoryId ?? string.Empty;
            ViewData["RepositoryId"] = resolvedRepositoryId;

            return View();
        }
    }
}
