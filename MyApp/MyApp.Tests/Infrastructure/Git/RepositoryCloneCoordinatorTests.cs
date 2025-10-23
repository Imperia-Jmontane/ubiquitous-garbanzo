using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using MyApp.Application.Abstractions;
using MyApp.Domain.Repositories;
using MyApp.Infrastructure.Git;
using Xunit;

#nullable enable

namespace MyApp.Tests.Infrastructure.Git
{
    public sealed class RepositoryCloneCoordinatorTests
    {
        [Fact]
        public void QueueClone_ShouldMarkOperationAsCanceledWhenCloneTaskIsCanceled()
        {
            Mock<ILocalRepositoryService> repositoryServiceMock = new Mock<ILocalRepositoryService>();
            repositoryServiceMock
                .Setup(service => service.GetRepositories())
                .Returns(new List<LocalRepository>());

            repositoryServiceMock
                .Setup(service => service.CloneRepositoryAsync(
                    It.IsAny<string>(),
                    It.IsAny<IProgress<RepositoryCloneProgress>>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.FromException<CloneRepositoryResult>(new TaskCanceledException()));

            repositoryServiceMock
                .Setup(service => service.RepositoryExists(It.IsAny<string>()))
                .Returns(false);

            Mock<ILogger<RepositoryCloneCoordinator>> loggerMock = new Mock<ILogger<RepositoryCloneCoordinator>>();

            RepositoryCloneCoordinator coordinator = new RepositoryCloneCoordinator(repositoryServiceMock.Object, loggerMock.Object);

            RepositoryCloneTicket ticket = coordinator.QueueClone("https://example.com/example.git");

            Assert.True(ticket.HasOperation);

            RepositoryCloneStatus latestStatus = null!;
            bool hasStatus = false;
            bool canceled = SpinWait.SpinUntil(
                () =>
                {
                    RepositoryCloneStatus? snapshot;

                    if (coordinator.TryGetStatus(ticket.OperationId, out snapshot) && snapshot != null)
                    {
                        latestStatus = snapshot;
                        hasStatus = true;

                        if (snapshot.State == RepositoryCloneState.Canceled)
                        {
                            return true;
                        }
                    }

                    return false;
                },
                TimeSpan.FromSeconds(2));

            Assert.True(canceled);
            Assert.True(hasStatus);
            Assert.Equal(RepositoryCloneState.Canceled, latestStatus.State);
            Assert.Equal("Repository clone was canceled.", latestStatus.Message);
        }

        [Fact]
        public void QueueClone_ShouldReturnAlreadyClonedTicketWhenRepositoryFolderExists()
        {
            Mock<ILocalRepositoryService> repositoryServiceMock = new Mock<ILocalRepositoryService>();
            repositoryServiceMock
                .Setup(service => service.RepositoryExists(It.IsAny<string>()))
                .Returns(true);

            Mock<ILogger<RepositoryCloneCoordinator>> loggerMock = new Mock<ILogger<RepositoryCloneCoordinator>>();

            RepositoryCloneCoordinator coordinator = new RepositoryCloneCoordinator(repositoryServiceMock.Object, loggerMock.Object);

            RepositoryCloneTicket ticket = coordinator.QueueClone("https://github.com/example/project.git");

            Assert.False(ticket.HasOperation);
            Assert.True(ticket.AlreadyCloned);
            Assert.False(ticket.Enqueued);

            repositoryServiceMock.Verify(service => service.CloneRepositoryAsync(
                It.IsAny<string>(),
                It.IsAny<IProgress<RepositoryCloneProgress>>(),
                It.IsAny<CancellationToken>()),
                Times.Never);
        }
    }
}
