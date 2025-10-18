using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using MyApp.Application.Abstractions;
using MyApp.Domain.Repositories;
using MyApp.Models;
using MyApp.Models.Home;

namespace MyApp.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ILocalRepositoryService _repositoryService;

        public HomeController(ILogger<HomeController> logger, ILocalRepositoryService repositoryService)
        {
            _logger = logger;
            _repositoryService = repositoryService;
        }

        public IActionResult Index()
        {
            IReadOnlyCollection<LocalRepository> repositories = _repositoryService.GetRepositories();
            string? addedRepositoryUrl = null;

            if (TempData != null && TempData.ContainsKey("RepositoryAdded"))
            {
                addedRepositoryUrl = TempData["RepositoryAdded"] as string;
            }
            HomeIndexViewModel viewModel = CreateHomeIndexViewModel(repositories, addedRepositoryUrl, new AddRepositoryRequest());
            return View(viewModel);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AddRepository(AddRepositoryRequest request)
        {
            if (!ModelState.IsValid)
            {
                IReadOnlyCollection<LocalRepository> repositories = _repositoryService.GetRepositories();
                HomeIndexViewModel invalidViewModel = CreateHomeIndexViewModel(repositories, null, request);
                return View("Index", invalidViewModel);
            }

            CloneRepositoryResult cloneResult = _repositoryService.CloneRepository(request.RepositoryUrl);

            if (!cloneResult.Succeeded)
            {
                string fieldKey = string.Format("{0}.{1}", nameof(HomeIndexViewModel.AddRepository), nameof(AddRepositoryRequest.RepositoryUrl));
                string errorMessage = string.IsNullOrWhiteSpace(cloneResult.Message) ? "Failed to clone repository." : cloneResult.Message;
                ModelState.AddModelError(fieldKey, errorMessage);
                IReadOnlyCollection<LocalRepository> repositories = _repositoryService.GetRepositories();
                HomeIndexViewModel invalidViewModel = CreateHomeIndexViewModel(repositories, null, request);
                return View("Index", invalidViewModel);
            }

            if (TempData != null)
            {
                string notification = cloneResult.AlreadyExists
                    ? string.Format("Repository already cloned: {0}", request.RepositoryUrl)
                    : request.RepositoryUrl;
                TempData["RepositoryAdded"] = notification;
            }
            return RedirectToAction(nameof(Index));
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        private static HomeIndexViewModel CreateHomeIndexViewModel(IReadOnlyCollection<LocalRepository> repositories, string? notification, AddRepositoryRequest addRepositoryRequest)
        {
            List<RepositoryListItemViewModel> repositoryViewModels = new List<RepositoryListItemViewModel>();

            foreach (LocalRepository repository in repositories)
            {
                RepositoryListItemViewModel repositoryViewModel = new RepositoryListItemViewModel(
                    repository.Name,
                    repository.FullPath,
                    repository.RemoteUrl,
                    repository.Branches);
                repositoryViewModels.Add(repositoryViewModel);
            }

            return new HomeIndexViewModel(repositoryViewModels, addRepositoryRequest, notification);
        }
    }
}
