using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using MyApp.Application.Authentication.Commands;
using MyApp.Application.Authentication.Interfaces;
using MyApp.Application.Authentication.Models;
using MyApp.Application.Common.Interfaces;
using MyApp.Domain.Entities;
using MyApp.Domain.ValueObjects;

namespace MyApp.Tests
{
    public sealed class RefreshGitHubTokenCommandHandlerTests
    {
        [Fact]
        public async Task Handle_Should_Refresh_Token()
        {
            Guid userId = Guid.NewGuid();
            GitHubIdentity identity = new GitHubIdentity("123", "octocat", "The Octocat", "https://avatars/github.png");
            GitHubAccountLink accountLink = new GitHubAccountLink(userId, identity, "secret/github/1", DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddHours(-6));
            GitHubToken existingToken = new GitHubToken("token", "refresh", DateTimeOffset.UtcNow.AddHours(-8), DateTimeOffset.UtcNow.AddHours(-4), new List<string> { "repo", "read:user" });

            RefreshGitHubTokenCommand command = new RefreshGitHubTokenCommand(userId);

            Mock<IGitHubAccountLinkRepository> accountLinkRepository = new Mock<IGitHubAccountLinkRepository>();
            accountLinkRepository.Setup(repository => repository.GetByUserIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(accountLink);
            accountLinkRepository.Setup(repository => repository.UpdateAsync(accountLink, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            accountLinkRepository.Setup(repository => repository.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            Mock<IGitCredentialStore> credentialStore = new Mock<IGitCredentialStore>();
            credentialStore.Setup(store => store.GetAsync("secret/github/1", It.IsAny<CancellationToken>())).ReturnsAsync(existingToken);
            credentialStore.Setup(store => store.UpdateAsync("secret/github/1", It.IsAny<GitHubToken>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            GitHubToken refreshedToken = new GitHubToken("new-token", "refresh", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(8), new List<string> { "repo", "read:user" });
            GitHubOAuthSession session = new GitHubOAuthSession(identity, refreshedToken);

            Mock<IGitHubOAuthClient> gitHubOAuthClient = new Mock<IGitHubOAuthClient>();
            gitHubOAuthClient.Setup(client => client.RefreshTokenAsync("refresh", It.IsAny<CancellationToken>())).ReturnsAsync(session);

            Mock<IDateTimeProvider> dateTimeProvider = new Mock<IDateTimeProvider>();
            dateTimeProvider.SetupGet(provider => provider.UtcNow).Returns(DateTimeOffset.UtcNow);

            ILogger<RefreshGitHubTokenCommandHandler> logger = Mock.Of<ILogger<RefreshGitHubTokenCommandHandler>>();

            RefreshGitHubTokenCommandHandler handler = new RefreshGitHubTokenCommandHandler(
                accountLinkRepository.Object,
                credentialStore.Object,
                gitHubOAuthClient.Object,
                dateTimeProvider.Object,
                logger);

            GitHubToken result = await handler.Handle(command, CancellationToken.None);

            result.AccessToken.Should().Be("new-token");
            credentialStore.Verify(store => store.UpdateAsync("secret/github/1", refreshedToken, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Handle_Should_Throw_When_NoRefreshToken()
        {
            Guid userId = Guid.NewGuid();
            GitHubIdentity identity = new GitHubIdentity("123", "octocat", "The Octocat", "https://avatars/github.png");
            GitHubAccountLink accountLink = new GitHubAccountLink(userId, identity, "secret/github/1", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
            GitHubToken existingToken = new GitHubToken("token", string.Empty, DateTimeOffset.UtcNow, null, new List<string> { "repo", "read:user" });

            Mock<IGitHubAccountLinkRepository> accountLinkRepository = new Mock<IGitHubAccountLinkRepository>();
            accountLinkRepository.Setup(repository => repository.GetByUserIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(accountLink);

            Mock<IGitCredentialStore> credentialStore = new Mock<IGitCredentialStore>();
            credentialStore.Setup(store => store.GetAsync("secret/github/1", It.IsAny<CancellationToken>())).ReturnsAsync(existingToken);

            Mock<IGitHubOAuthClient> gitHubOAuthClient = new Mock<IGitHubOAuthClient>();
            Mock<IDateTimeProvider> dateTimeProvider = new Mock<IDateTimeProvider>();
            ILogger<RefreshGitHubTokenCommandHandler> logger = Mock.Of<ILogger<RefreshGitHubTokenCommandHandler>>();

            RefreshGitHubTokenCommandHandler handler = new RefreshGitHubTokenCommandHandler(
                accountLinkRepository.Object,
                credentialStore.Object,
                gitHubOAuthClient.Object,
                dateTimeProvider.Object,
                logger);

            Func<Task> action = async () => await handler.Handle(new RefreshGitHubTokenCommand(userId), CancellationToken.None);

            await action.Should().ThrowAsync<InvalidOperationException>();
        }
    }
}
