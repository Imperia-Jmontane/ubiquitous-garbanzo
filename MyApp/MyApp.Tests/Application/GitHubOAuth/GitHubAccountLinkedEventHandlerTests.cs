#nullable enable
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using MyApp.Application.Abstractions;
using MyApp.Application.GitHubOAuth.Events;
using MyApp.Domain.Observability;
using Xunit;

namespace MyApp.Tests.Application.GitHubOAuth
{
    public sealed class GitHubAccountLinkedEventHandlerTests
    {
        [Fact]
        public async Task Handle_ShouldPersistAuditEntry()
        {
            Mock<IAuditTrailRepository> repositoryMock = new Mock<IAuditTrailRepository>();
            Mock<ILogger<GitHubAccountLinkedEventHandler>> loggerMock = new Mock<ILogger<GitHubAccountLinkedEventHandler>>();

            AuditTrailEntry? capturedEntry = null;
            repositoryMock.Setup(repository => repository.AddAsync(It.IsAny<AuditTrailEntry>(), It.IsAny<CancellationToken>()))
                .Callback<AuditTrailEntry, CancellationToken>((entry, token) => capturedEntry = entry)
                .Returns(Task.CompletedTask);

            GitHubAccountLinkedEventHandler handler = new GitHubAccountLinkedEventHandler(repositoryMock.Object, loggerMock.Object);

            Guid userId = Guid.NewGuid();
            IReadOnlyCollection<string> scopes = new List<string> { "repo", "workflow" };
            GitHubAccountLinkedEvent domainEvent = new GitHubAccountLinkedEvent(userId, "GitHub", scopes, true, true, DateTimeOffset.UtcNow, "corr-1");

            await handler.Handle(domainEvent, CancellationToken.None);

            repositoryMock.Verify(repository => repository.AddAsync(It.IsAny<AuditTrailEntry>(), It.IsAny<CancellationToken>()), Times.Once);
            capturedEntry.Should().NotBeNull();
            capturedEntry!.UserId.Should().Be(userId);
            capturedEntry.Provider.Should().Be("GitHub");
            capturedEntry.EventType.Should().Be("GitHubAccountLinked");
            JsonDocument document = JsonDocument.Parse(capturedEntry.Payload);
            document.RootElement.GetProperty("IsNewConnection").GetBoolean().Should().BeTrue();
            document.RootElement.GetProperty("CanClone").GetBoolean().Should().BeTrue();
        }

        [Fact]
        public async Task Handle_ShouldPropagateException_WhenRepositoryFails()
        {
            Mock<IAuditTrailRepository> repositoryMock = new Mock<IAuditTrailRepository>();
            Mock<ILogger<GitHubAccountLinkedEventHandler>> loggerMock = new Mock<ILogger<GitHubAccountLinkedEventHandler>>();

            repositoryMock.Setup(repository => repository.AddAsync(It.IsAny<AuditTrailEntry>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("failure"));

            GitHubAccountLinkedEventHandler handler = new GitHubAccountLinkedEventHandler(repositoryMock.Object, loggerMock.Object);

            GitHubAccountLinkedEvent domainEvent = new GitHubAccountLinkedEvent(Guid.NewGuid(), "GitHub", new List<string>(), false, false, DateTimeOffset.UtcNow, string.Empty);

            await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(domainEvent, CancellationToken.None));
        }
    }
}
