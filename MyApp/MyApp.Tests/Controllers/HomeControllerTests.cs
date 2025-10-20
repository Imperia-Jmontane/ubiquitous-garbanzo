using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging;
using Moq;
using MyApp.Application.Abstractions;
using MyApp.Controllers;
using MyApp.Domain.Repositories;
using MyApp.Models.Home;
using Xunit;

namespace MyApp.Tests.Controllers
{
    public sealed class HomeControllerTests
    {
        [Fact]
        public void Index_ShouldReturnRepositories()
        {
            List<string> branches = new List<string> { "main", "develop" };
            List<LocalRepository> repositories = new List<LocalRepository>
            {
                new LocalRepository("ubiquitous-garbanzo", "/tmp/ubiquitous-garbanzo", "https://github.com/Imperia-Jmontane/ubiquitous-garbanzo", branches, false, false)
            };

            Mock<ILocalRepositoryService> repositoryServiceMock = new Mock<ILocalRepositoryService>();
            repositoryServiceMock.Setup(service => service.GetRepositories()).Returns(repositories);
            Mock<IRepositoryCloneCoordinator> cloneCoordinatorMock = new Mock<IRepositoryCloneCoordinator>();
            cloneCoordinatorMock.Setup(coordinator => coordinator.GetActiveClones()).Returns(new List<RepositoryCloneStatus>());
            Mock<ILogger<HomeController>> loggerMock = new Mock<ILogger<HomeController>>();

            HomeController controller = new HomeController(loggerMock.Object, repositoryServiceMock.Object, cloneCoordinatorMock.Object);

            IActionResult result = controller.Index();

            ViewResult viewResult = Assert.IsType<ViewResult>(result);
            HomeIndexViewModel viewModel = Assert.IsType<HomeIndexViewModel>(viewResult.Model);
            Assert.True(viewModel.HasRepositories);
            RepositoryListItemViewModel repositoryViewModel = viewModel.Repositories.First();
            Assert.Equal("ubiquitous-garbanzo", repositoryViewModel.Name);
            Assert.Contains("develop", repositoryViewModel.Branches);
        }

        [Fact]
        public void AddRepository_ShouldReturnViewWhenModelInvalid()
        {
            Mock<ILocalRepositoryService> repositoryServiceMock = new Mock<ILocalRepositoryService>();
            repositoryServiceMock.Setup(service => service.GetRepositories()).Returns(new List<LocalRepository>());
            Mock<IRepositoryCloneCoordinator> cloneCoordinatorMock = new Mock<IRepositoryCloneCoordinator>();
            cloneCoordinatorMock.Setup(coordinator => coordinator.GetActiveClones()).Returns(new List<RepositoryCloneStatus>());
            Mock<ILogger<HomeController>> loggerMock = new Mock<ILogger<HomeController>>();

            HomeController controller = new HomeController(loggerMock.Object, repositoryServiceMock.Object, cloneCoordinatorMock.Object);
            controller.ModelState.AddModelError("RepositoryUrl", "Required");

            AddRepositoryRequest request = new AddRepositoryRequest();

            IActionResult result = controller.AddRepository(request);

            ViewResult viewResult = Assert.IsType<ViewResult>(result);
            HomeIndexViewModel viewModel = Assert.IsType<HomeIndexViewModel>(viewResult.Model);
            Assert.False(viewModel.HasRepositories);
        }

        [Fact]
        public void AddRepository_ShouldRedirectWhenValid()
        {
            Mock<ILocalRepositoryService> repositoryServiceMock = new Mock<ILocalRepositoryService>();
            repositoryServiceMock.Setup(service => service.GetRepositories()).Returns(new List<LocalRepository>());
            Mock<IRepositoryCloneCoordinator> cloneCoordinatorMock = new Mock<IRepositoryCloneCoordinator>();
            Mock<ILogger<HomeController>> loggerMock = new Mock<ILogger<HomeController>>();

            Guid operationId = Guid.NewGuid();
            RepositoryCloneTicket ticket = new RepositoryCloneTicket(operationId, "https://github.com/Imperia-Jmontane/ubiquitous-garbanzo", false, true);
            cloneCoordinatorMock.Setup(coordinator => coordinator.QueueClone(It.IsAny<string>())).Returns(ticket);

            HomeController controller = new HomeController(loggerMock.Object, repositoryServiceMock.Object, cloneCoordinatorMock.Object);
            DefaultHttpContext httpContext = new DefaultHttpContext();
            Mock<ITempDataProvider> tempDataProviderMock = new Mock<ITempDataProvider>();
            controller.TempData = new TempDataDictionary(httpContext, tempDataProviderMock.Object);

            AddRepositoryRequest request = new AddRepositoryRequest
            {
                RepositoryUrl = "https://github.com/Imperia-Jmontane/ubiquitous-garbanzo"
            };

            IActionResult result = controller.AddRepository(request);

            RedirectToActionResult redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal(nameof(HomeController.Index), redirectResult.ActionName);
        }

        [Fact]
        public void AddRepository_ShouldSignalAlreadyCloned()
        {
            Mock<ILocalRepositoryService> repositoryServiceMock = new Mock<ILocalRepositoryService>();
            repositoryServiceMock.Setup(service => service.GetRepositories()).Returns(new List<LocalRepository>());
            Mock<IRepositoryCloneCoordinator> cloneCoordinatorMock = new Mock<IRepositoryCloneCoordinator>();
            Mock<ILogger<HomeController>> loggerMock = new Mock<ILogger<HomeController>>();

            RepositoryCloneTicket ticket = new RepositoryCloneTicket(Guid.Empty, "https://github.com/Imperia-Jmontane/ubiquitous-garbanzo", true, false);
            cloneCoordinatorMock.Setup(coordinator => coordinator.QueueClone(It.IsAny<string>())).Returns(ticket);

            HomeController controller = new HomeController(loggerMock.Object, repositoryServiceMock.Object, cloneCoordinatorMock.Object);
            DefaultHttpContext httpContext = new DefaultHttpContext();
            Mock<ITempDataProvider> tempDataProviderMock = new Mock<ITempDataProvider>();
            controller.TempData = new TempDataDictionary(httpContext, tempDataProviderMock.Object);

            AddRepositoryRequest request = new AddRepositoryRequest
            {
                RepositoryUrl = "https://github.com/Imperia-Jmontane/ubiquitous-garbanzo"
            };

            IActionResult result = controller.AddRepository(request);

            RedirectToActionResult redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal(nameof(HomeController.Index), redirectResult.ActionName);
            Assert.True(controller.TempData.ContainsKey("RepositoryAdded"));
        }

        [Fact]
        public void Index_ShouldIncludeActiveCloneStatuses()
        {
            List<LocalRepository> repositories = new List<LocalRepository>();
            Mock<ILocalRepositoryService> repositoryServiceMock = new Mock<ILocalRepositoryService>();
            repositoryServiceMock.Setup(service => service.GetRepositories()).Returns(repositories);
            Mock<IRepositoryCloneCoordinator> cloneCoordinatorMock = new Mock<IRepositoryCloneCoordinator>();
            Mock<ILogger<HomeController>> loggerMock = new Mock<ILogger<HomeController>>();

            Guid operationId = Guid.NewGuid();
            RepositoryCloneStatus status = new RepositoryCloneStatus(operationId, "https://github.com/example/repo", RepositoryCloneState.Running, 42.0, "Receiving objects", string.Empty, DateTimeOffset.UtcNow);
            cloneCoordinatorMock.Setup(coordinator => coordinator.GetActiveClones()).Returns(new List<RepositoryCloneStatus> { status });

            HomeController controller = new HomeController(loggerMock.Object, repositoryServiceMock.Object, cloneCoordinatorMock.Object);

            IActionResult result = controller.Index();

            ViewResult viewResult = Assert.IsType<ViewResult>(result);
            HomeIndexViewModel viewModel = Assert.IsType<HomeIndexViewModel>(viewResult.Model);
            Assert.True(viewModel.IsCloneInProgress);
            CloneProgressViewModel cloneProgress = Assert.Single(viewModel.CloneProgressItems);
            Assert.Equal(42.0, cloneProgress.Percentage);
            Assert.Equal("Receiving objects", cloneProgress.Stage);
        }
    }
}
