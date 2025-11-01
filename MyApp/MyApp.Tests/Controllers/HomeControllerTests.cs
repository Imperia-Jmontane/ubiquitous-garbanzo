using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging;
using Moq;
using MediatR;
using MyApp.Application.Abstractions;
using MyApp.Application.Profile.DTOs;
using MyApp.Application.Profile.Queries.GetFlowBranchPreference;
using MyApp.Controllers;
using MyApp.Domain.Repositories;
using MyApp.Models.Home;
using Xunit;

namespace MyApp.Tests.Controllers
{
    public sealed class HomeControllerTests
    {
        [Fact]
        public async Task Index_ShouldReturnRepositories()
        {
            List<RepositoryBranch> branches = new List<RepositoryBranch>
            {
                new RepositoryBranch("main", true, true, false, "origin/main", 0, 0),
                new RepositoryBranch("develop", false, true, false, "origin/develop", 1, 0)
            };
            List<LocalRepository> repositories = new List<LocalRepository>
            {
                new LocalRepository("ubiquitous-garbanzo", "/tmp/ubiquitous-garbanzo", "https://github.com/Imperia-Jmontane/ubiquitous-garbanzo", branches, false, false, DateTimeOffset.UtcNow)
            };

            Mock<ILocalRepositoryService> repositoryServiceMock = new Mock<ILocalRepositoryService>();
            repositoryServiceMock.Setup(service => service.GetRepositories()).Returns(repositories);
            Mock<IRepositoryCloneCoordinator> cloneCoordinatorMock = new Mock<IRepositoryCloneCoordinator>();
            cloneCoordinatorMock.Setup(coordinator => coordinator.GetActiveClones()).Returns(new List<RepositoryCloneStatus>());
            Mock<ILogger<HomeController>> loggerMock = new Mock<ILogger<HomeController>>();
            Mock<IMediator> mediatorMock = new Mock<IMediator>();
            mediatorMock
                .Setup(mediator => mediator.Send(It.IsAny<GetFlowBranchPreferenceQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new FlowBranchPreferenceDto(false));

            HomeController controller = new HomeController(loggerMock.Object, repositoryServiceMock.Object, cloneCoordinatorMock.Object, mediatorMock.Object);

            IActionResult result = await controller.Index();

            ViewResult viewResult = Assert.IsType<ViewResult>(result);
            HomeIndexViewModel viewModel = Assert.IsType<HomeIndexViewModel>(viewResult.Model);
            Assert.True(viewModel.HasRepositories);
            RepositoryListItemViewModel repositoryViewModel = viewModel.Repositories.First();
            Assert.Equal("ubiquitous-garbanzo", repositoryViewModel.Name);
            Assert.Contains(repositoryViewModel.Branches, branch => branch.Name == "develop");
            Assert.False(repositoryViewModel.HasUnsyncedChanges);
        }

        [Fact]
        public async Task AddRepository_ShouldReturnViewWhenModelInvalid()
        {
            Mock<ILocalRepositoryService> repositoryServiceMock = new Mock<ILocalRepositoryService>();
            repositoryServiceMock.Setup(service => service.GetRepositories()).Returns(new List<LocalRepository>());
            Mock<IRepositoryCloneCoordinator> cloneCoordinatorMock = new Mock<IRepositoryCloneCoordinator>();
            cloneCoordinatorMock.Setup(coordinator => coordinator.GetActiveClones()).Returns(new List<RepositoryCloneStatus>());
            Mock<ILogger<HomeController>> loggerMock = new Mock<ILogger<HomeController>>();
            Mock<IMediator> mediatorMock = new Mock<IMediator>();
            mediatorMock
                .Setup(mediator => mediator.Send(It.IsAny<GetFlowBranchPreferenceQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new FlowBranchPreferenceDto(false));

            HomeController controller = new HomeController(loggerMock.Object, repositoryServiceMock.Object, cloneCoordinatorMock.Object, mediatorMock.Object);
            controller.ModelState.AddModelError("RepositoryUrl", "Required");

            AddRepositoryRequest request = new AddRepositoryRequest();

            IActionResult result = await controller.AddRepository(request);

            ViewResult viewResult = Assert.IsType<ViewResult>(result);
            HomeIndexViewModel viewModel = Assert.IsType<HomeIndexViewModel>(viewResult.Model);
            Assert.False(viewModel.HasRepositories);
        }

        [Fact]
        public async Task AddRepository_ShouldRedirectWhenValid()
        {
            Mock<ILocalRepositoryService> repositoryServiceMock = new Mock<ILocalRepositoryService>();
            repositoryServiceMock.Setup(service => service.GetRepositories()).Returns(new List<LocalRepository>());
            Mock<IRepositoryCloneCoordinator> cloneCoordinatorMock = new Mock<IRepositoryCloneCoordinator>();
            Mock<ILogger<HomeController>> loggerMock = new Mock<ILogger<HomeController>>();
            Mock<IMediator> mediatorMock = new Mock<IMediator>();
            mediatorMock
                .Setup(mediator => mediator.Send(It.IsAny<GetFlowBranchPreferenceQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new FlowBranchPreferenceDto(false));

            Guid operationId = Guid.NewGuid();
            RepositoryCloneTicket ticket = new RepositoryCloneTicket(operationId, "https://github.com/Imperia-Jmontane/ubiquitous-garbanzo", false, true);
            cloneCoordinatorMock.Setup(coordinator => coordinator.QueueClone(It.IsAny<string>())).Returns(ticket);

            HomeController controller = new HomeController(loggerMock.Object, repositoryServiceMock.Object, cloneCoordinatorMock.Object, mediatorMock.Object);
            DefaultHttpContext httpContext = new DefaultHttpContext();
            Mock<ITempDataProvider> tempDataProviderMock = new Mock<ITempDataProvider>();
            controller.TempData = new TempDataDictionary(httpContext, tempDataProviderMock.Object);

            AddRepositoryRequest request = new AddRepositoryRequest
            {
                RepositoryUrl = "https://github.com/Imperia-Jmontane/ubiquitous-garbanzo"
            };

            IActionResult result = await controller.AddRepository(request);

            RedirectToActionResult redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal(nameof(HomeController.Index), redirectResult.ActionName);
        }

        [Fact]
        public async Task AddRepository_ShouldSignalAlreadyCloned()
        {
            Mock<ILocalRepositoryService> repositoryServiceMock = new Mock<ILocalRepositoryService>();
            repositoryServiceMock.Setup(service => service.GetRepositories()).Returns(new List<LocalRepository>());
            Mock<IRepositoryCloneCoordinator> cloneCoordinatorMock = new Mock<IRepositoryCloneCoordinator>();
            Mock<ILogger<HomeController>> loggerMock = new Mock<ILogger<HomeController>>();
            Mock<IMediator> mediatorMock = new Mock<IMediator>();
            mediatorMock
                .Setup(mediator => mediator.Send(It.IsAny<GetFlowBranchPreferenceQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new FlowBranchPreferenceDto(false));

            RepositoryCloneTicket ticket = new RepositoryCloneTicket(Guid.Empty, "https://github.com/Imperia-Jmontane/ubiquitous-garbanzo", true, false);
            cloneCoordinatorMock.Setup(coordinator => coordinator.QueueClone(It.IsAny<string>())).Returns(ticket);

            HomeController controller = new HomeController(loggerMock.Object, repositoryServiceMock.Object, cloneCoordinatorMock.Object, mediatorMock.Object);
            DefaultHttpContext httpContext = new DefaultHttpContext();
            Mock<ITempDataProvider> tempDataProviderMock = new Mock<ITempDataProvider>();
            controller.TempData = new TempDataDictionary(httpContext, tempDataProviderMock.Object);

            AddRepositoryRequest request = new AddRepositoryRequest
            {
                RepositoryUrl = "https://github.com/Imperia-Jmontane/ubiquitous-garbanzo"
            };

            IActionResult result = await controller.AddRepository(request);

            RedirectToActionResult redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal(nameof(HomeController.Index), redirectResult.ActionName);
            Assert.True(controller.TempData.ContainsKey("RepositoryAdded"));
        }

        [Fact]
        public async Task Index_ShouldIncludeActiveCloneStatuses()
        {
            List<LocalRepository> repositories = new List<LocalRepository>();
            Mock<ILocalRepositoryService> repositoryServiceMock = new Mock<ILocalRepositoryService>();
            repositoryServiceMock.Setup(service => service.GetRepositories()).Returns(repositories);
            Mock<IRepositoryCloneCoordinator> cloneCoordinatorMock = new Mock<IRepositoryCloneCoordinator>();
            Mock<ILogger<HomeController>> loggerMock = new Mock<ILogger<HomeController>>();
            Mock<IMediator> mediatorMock = new Mock<IMediator>();
            mediatorMock
                .Setup(mediator => mediator.Send(It.IsAny<GetFlowBranchPreferenceQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new FlowBranchPreferenceDto(false));

            Guid operationId = Guid.NewGuid();
            RepositoryCloneStatus status = new RepositoryCloneStatus(operationId, "https://github.com/example/repo", RepositoryCloneState.Running, 42.0, "Receiving objects", string.Empty, DateTimeOffset.UtcNow);
            cloneCoordinatorMock.Setup(coordinator => coordinator.GetActiveClones()).Returns(new List<RepositoryCloneStatus> { status });

            HomeController controller = new HomeController(loggerMock.Object, repositoryServiceMock.Object, cloneCoordinatorMock.Object, mediatorMock.Object);

            IActionResult result = await controller.Index();

            ViewResult viewResult = Assert.IsType<ViewResult>(result);
            HomeIndexViewModel viewModel = Assert.IsType<HomeIndexViewModel>(viewResult.Model);
            Assert.True(viewModel.IsCloneInProgress);
            CloneProgressViewModel cloneProgress = Assert.Single(viewModel.CloneProgressItems);
            Assert.Equal(42.0, cloneProgress.Percentage);
            Assert.Equal("Receiving objects", cloneProgress.Stage);
        }
    }
}
