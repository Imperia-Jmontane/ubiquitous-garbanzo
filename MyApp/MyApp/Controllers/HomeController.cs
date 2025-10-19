using System;
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
        private readonly IRepositoryCloneCoordinator _cloneCoordinator;

        public HomeController(ILogger<HomeController> logger, ILocalRepositoryService repositoryService, IRepositoryCloneCoordinator cloneCoordinator)
        {
            _logger = logger;
            _repositoryService = repositoryService;
            _cloneCoordinator = cloneCoordinator;
        }

        public IActionResult Index()
        {
            IReadOnlyCollection<LocalRepository> repositories = _repositoryService.GetRepositories();
            string? addedRepositoryUrl = null;
            CloneProgressViewModel cloneProgress = CloneProgressViewModel.CreateInactive();

            if (TempData != null)
            {
                if (TempData.ContainsKey("RepositoryAdded"))
                {
                    addedRepositoryUrl = TempData["RepositoryAdded"] as string;
                }

                if (TempData.ContainsKey("RepositoryCloneOperationId"))
                {
                    object? operationValue = TempData["RepositoryCloneOperationId"];
                    Guid operationId;

                    if (operationValue is string operationText && Guid.TryParse(operationText, out operationId))
                    {
                        RepositoryCloneStatus? status;

                        if (_cloneCoordinator.TryGetStatus(operationId, out status) && status != null)
                        {
                            cloneProgress = new CloneProgressViewModel(true, status.OperationId, status.RepositoryUrl, status.Percentage, status.Stage, status.Message);
                        }
                        else
                        {
                            cloneProgress = new CloneProgressViewModel(true, operationId, string.Empty, 0.0, "Queued", string.Empty);
                        }
                    }
                }
            }

            HomeIndexViewModel viewModel = CreateHomeIndexViewModel(repositories, addedRepositoryUrl, new AddRepositoryRequest(), cloneProgress);
            return View(viewModel);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AddRepository([Bind(Prefix = nameof(HomeIndexViewModel.AddRepository))] AddRepositoryRequest request)
        {
            if (!ModelState.IsValid)
            {
                IReadOnlyCollection<LocalRepository> repositories = _repositoryService.GetRepositories();
                HomeIndexViewModel invalidViewModel = CreateHomeIndexViewModel(repositories, null, request, CloneProgressViewModel.CreateInactive());
                return View("Index", invalidViewModel);
            }

            RepositoryCloneTicket ticket = _cloneCoordinator.QueueClone(request.RepositoryUrl);

            if (TempData != null)
            {
                string notification;

                if (ticket.AlreadyCloned)
                {
                    notification = string.Format("Repository already cloned: {0}", request.RepositoryUrl);
                }
                else if (!ticket.Enqueued && ticket.HasOperation)
                {
                    notification = string.Format("Repository clone already in progress: {0}", request.RepositoryUrl);
                }
                else
                {
                    notification = request.RepositoryUrl;
                }

                TempData["RepositoryAdded"] = notification;

                if (ticket.HasOperation)
                {
                    TempData["RepositoryCloneOperationId"] = ticket.OperationId.ToString();
                }
            }
            return RedirectToAction(nameof(Index));
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        private static HomeIndexViewModel CreateHomeIndexViewModel(IReadOnlyCollection<LocalRepository> repositories, string? notification, AddRepositoryRequest addRepositoryRequest, CloneProgressViewModel cloneProgress)
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

            return new HomeIndexViewModel(repositoryViewModels, addRepositoryRequest, notification, cloneProgress);
        }
    }
}
