#nullable enable
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Moq;
using MyApp.Application.Abstractions;
using MyApp.Controllers.Api;
using MyApp.Domain.Repositories;
using Xunit;

namespace MyApp.Tests.Controllers.Api
{
    public sealed class RepositoriesControllerTests
    {
        [Fact]
        public void GetRemoteBranches_ShouldReturnResults_WhenQueryIsEmpty()
        {
            string repositoryPath = "C:/projects/sample";
            List<RepositoryRemoteBranch> branches = new List<RepositoryRemoteBranch>
            {
                new RepositoryRemoteBranch("origin", "main", true)
            };
            RemoteBranchQueryResult serviceResult = new RemoteBranchQueryResult(true, "Loaded", branches);

            Mock<ILocalRepositoryService> serviceMock = new Mock<ILocalRepositoryService>();
            serviceMock.Setup(service => service.GetRemoteBranches(repositoryPath, string.Empty)).Returns(serviceResult);

            RepositoriesController controller = new RepositoriesController(serviceMock.Object);

            ActionResult<RepositoriesController.RemoteBranchesResponse> result = controller.GetRemoteBranches(repositoryPath, string.Empty);
            OkObjectResult okResult = Assert.IsType<OkObjectResult>(result.Result);
            RepositoriesController.RemoteBranchesResponse response = Assert.IsType<RepositoriesController.RemoteBranchesResponse>(okResult.Value);
            Assert.True(response.Succeeded);
            Assert.Single(response.Branches);
            serviceMock.Verify(service => service.GetRemoteBranches(repositoryPath, string.Empty), Times.Once);
        }

        [Fact]
        public void GetRemoteBranches_ShouldNormalizeNullQuery_ToEmptyString()
        {
            string repositoryPath = "C:/projects/sample";
            List<RepositoryRemoteBranch> branches = new List<RepositoryRemoteBranch>
            {
                new RepositoryRemoteBranch("origin", "develop", false)
            };
            RemoteBranchQueryResult serviceResult = new RemoteBranchQueryResult(true, "Loaded", branches);

            Mock<ILocalRepositoryService> serviceMock = new Mock<ILocalRepositoryService>();
            serviceMock.Setup(service => service.GetRemoteBranches(repositoryPath, string.Empty)).Returns(serviceResult);

            RepositoriesController controller = new RepositoriesController(serviceMock.Object);

            ActionResult<RepositoriesController.RemoteBranchesResponse> result = controller.GetRemoteBranches(repositoryPath, null);
            OkObjectResult okResult = Assert.IsType<OkObjectResult>(result.Result);
            RepositoriesController.RemoteBranchesResponse response = Assert.IsType<RepositoriesController.RemoteBranchesResponse>(okResult.Value);
            Assert.True(response.Succeeded);
            Assert.Single(response.Branches);
            serviceMock.Verify(service => service.GetRemoteBranches(repositoryPath, string.Empty), Times.Once);
        }
    }
}
