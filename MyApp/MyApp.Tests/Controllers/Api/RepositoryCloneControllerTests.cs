#nullable enable
using System;
using Microsoft.AspNetCore.Mvc;
using Moq;
using MyApp.Application.Abstractions;
using MyApp.Controllers.Api;
using Xunit;

namespace MyApp.Tests.Controllers.Api
{
    public sealed class RepositoryCloneControllerTests
    {
        [Fact]
        public void QueueClone_ShouldReturnAcceptedForNewOperation()
        {
            Guid operationId = Guid.NewGuid();
            RepositoryCloneTicket ticket = new RepositoryCloneTicket(operationId, "https://github.com/example/repo.git", false, true);
            RepositoryCloneStatus? status = new RepositoryCloneStatus(operationId, "https://github.com/example/repo.git", RepositoryCloneState.Queued, 0.0, "Queued", string.Empty, DateTimeOffset.UtcNow);

            Mock<IRepositoryCloneCoordinator> coordinatorMock = new Mock<IRepositoryCloneCoordinator>();
            coordinatorMock.Setup(coordinator => coordinator.QueueClone(It.IsAny<string>())).Returns(ticket);
            coordinatorMock.Setup(coordinator => coordinator.TryGetStatus(operationId, out status)).Returns(true);

            RepositoryCloneController controller = new RepositoryCloneController(coordinatorMock.Object);

            RepositoryCloneController.QueueCloneRequest request = new RepositoryCloneController.QueueCloneRequest
            {
                RepositoryUrl = "https://github.com/example/repo.git"
            };

            ActionResult<RepositoryCloneController.QueueCloneResponse> result = controller.QueueClone(request);
            AcceptedResult accepted = Assert.IsType<AcceptedResult>(result.Result);
            RepositoryCloneController.QueueCloneResponse response = Assert.IsType<RepositoryCloneController.QueueCloneResponse>(accepted.Value);
            Assert.Equal(operationId, response.OperationId);
            Assert.True(response.Enqueued);
            Assert.NotNull(response.Status);
            Assert.Equal("Queued", response.Status?.State);
        }

        [Fact]
        public void QueueClone_ShouldReturnOkWhenAlreadyCloned()
        {
            RepositoryCloneTicket ticket = new RepositoryCloneTicket(Guid.Empty, "https://github.com/example/repo.git", true, false);

            Mock<IRepositoryCloneCoordinator> coordinatorMock = new Mock<IRepositoryCloneCoordinator>();
            coordinatorMock.Setup(coordinator => coordinator.QueueClone(It.IsAny<string>())).Returns(ticket);

            RepositoryCloneController controller = new RepositoryCloneController(coordinatorMock.Object);

            RepositoryCloneController.QueueCloneRequest request = new RepositoryCloneController.QueueCloneRequest
            {
                RepositoryUrl = "https://github.com/example/repo.git"
            };

            ActionResult<RepositoryCloneController.QueueCloneResponse> result = controller.QueueClone(request);
            OkObjectResult okResult = Assert.IsType<OkObjectResult>(result.Result);
            RepositoryCloneController.QueueCloneResponse response = Assert.IsType<RepositoryCloneController.QueueCloneResponse>(okResult.Value);
            Assert.True(response.AlreadyCloned);
            Assert.False(response.Enqueued);
            Assert.NotNull(response.Status);
            Assert.Equal("Completed", response.Status?.State);
        }

        [Fact]
        public void GetCloneStatus_ShouldReturnNotFoundWhenMissing()
        {
            Mock<IRepositoryCloneCoordinator> coordinatorMock = new Mock<IRepositoryCloneCoordinator>();
            RepositoryCloneStatus? status = null;
            coordinatorMock.Setup(coordinator => coordinator.TryGetStatus(It.IsAny<Guid>(), out status)).Returns(false);

            RepositoryCloneController controller = new RepositoryCloneController(coordinatorMock.Object);

            ActionResult<RepositoryCloneController.RepositoryCloneStatusResponse> result = controller.GetCloneStatus(Guid.NewGuid());
            Assert.IsType<NotFoundResult>(result.Result);
        }
    }
}
