using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using MyApp.Application.Authentication.Commands;
using MyApp.Application.Authentication.Interfaces;
using MyApp.Application.Authentication.Models;
using MyApp.Application.Common.Interfaces;
using MyApp.Domain.Entities;
using MyApp.Domain.Events;
using MyApp.Domain.ValueObjects;

namespace MyApp.Tests
{
    public sealed class LinkGitHubAccountCommandHandlerTests
    {
        [Fact]
        public async Task Handle_Should_Link_GitHubAccount()
        {
            Guid userId = Guid.NewGuid();
            string state = "state-token";
            DateTimeOffset now = DateTimeOffset.UtcNow;

            UserExternalLogin existingLogin = new UserExternalLogin(Guid.NewGuid(), userId, "GitHub", string.Empty, state, string.Empty, now);
            LinkGitHubAccountCommand command = new LinkGitHubAccountCommand(userId, "code", state, "https://app.example.com/callback");

            Mock<IUserExternalLoginRepository> externalLoginRepository = new Mock<IUserExternalLoginRepository>();
            externalLoginRepository.Setup(repository => repository.FindByStateAsync(state, It.IsAny<CancellationToken>())).ReturnsAsync(existingLogin);
            externalLoginRepository.Setup(repository => repository.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            Mock<IGitHubAccountLinkRepository> accountLinkRepository = new Mock<IGitHubAccountLinkRepository>();
            accountLinkRepository.Setup(repository => repository.GetByUserIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync((GitHubAccountLink?)null);
            accountLinkRepository.Setup(repository => repository.AddAsync(It.IsAny<GitHubAccountLink>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            accountLinkRepository.Setup(repository => repository.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            Mock<IGitCredentialStore> credentialStore = new Mock<IGitCredentialStore>();
            credentialStore.Setup(store => store.StoreAsync(userId, It.IsAny<GitHubToken>(), It.IsAny<CancellationToken>())).ReturnsAsync("secret/github/1");

            GitHubToken token = new GitHubToken("token-value", "refresh-token", now, now.AddHours(1), new List<string> { "repo", "read:user" });
            GitHubIdentity identity = new GitHubIdentity("123", "octocat", "The Octocat", "https://avatars/github.png");
            GitHubOAuthSession session = new GitHubOAuthSession(identity, token);

            Mock<IGitHubOAuthClient> gitHubOAuthClient = new Mock<IGitHubOAuthClient>();
            gitHubOAuthClient.Setup(client => client.ExchangeCodeForTokenAsync("code", "https://app.example.com/callback", It.IsAny<CancellationToken>())).ReturnsAsync(session);

            Mock<IDateTimeProvider> dateTimeProvider = new Mock<IDateTimeProvider>();
            dateTimeProvider.SetupGet(provider => provider.UtcNow).Returns(now);

            Mock<IPublisher> publisher = new Mock<IPublisher>();
            publisher.Setup(p => p.Publish(It.IsAny<GitHubAccountLinkedEvent>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            Mock<IGitHubLinkMetrics> metrics = new Mock<IGitHubLinkMetrics>();
            ILogger<LinkGitHubAccountCommandHandler> logger = Mock.Of<ILogger<LinkGitHubAccountCommandHandler>>();

            LinkGitHubAccountCommandHandler handler = new LinkGitHubAccountCommandHandler(
                externalLoginRepository.Object,
                accountLinkRepository.Object,
                credentialStore.Object,
                gitHubOAuthClient.Object,
                dateTimeProvider.Object,
                publisher.Object,
                metrics.Object,
                logger);

            LinkGitHubAccountResult result = await handler.Handle(command, CancellationToken.None);

            result.CanClone.Should().BeTrue();
            result.AccountLink.Identity.Login.Should().Be("octocat");
            metrics.Verify(m => m.RecordLinkSuccess(userId), Times.Once);
            publisher.Verify(p => p.Publish(It.IsAny<GitHubAccountLinkedEvent>(), It.IsAny<CancellationToken>()), Times.Once);
            credentialStore.Verify(store => store.StoreAsync(userId, It.IsAny<GitHubToken>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Handle_Should_RecordFailure_When_SessionMissing()
        {
            Guid userId = Guid.NewGuid();
            LinkGitHubAccountCommand command = new LinkGitHubAccountCommand(userId, "code", "missing-state", "https://app.example.com/callback");

            Mock<IUserExternalLoginRepository> externalLoginRepository = new Mock<IUserExternalLoginRepository>();
            externalLoginRepository.Setup(repository => repository.FindByStateAsync("missing-state", It.IsAny<CancellationToken>())).ReturnsAsync((UserExternalLogin?)null);

            Mock<IGitHubAccountLinkRepository> accountLinkRepository = new Mock<IGitHubAccountLinkRepository>();
            Mock<IGitCredentialStore> credentialStore = new Mock<IGitCredentialStore>();
            Mock<IGitHubOAuthClient> gitHubOAuthClient = new Mock<IGitHubOAuthClient>();
            Mock<IDateTimeProvider> dateTimeProvider = new Mock<IDateTimeProvider>();
            Mock<IPublisher> publisher = new Mock<IPublisher>();
            Mock<IGitHubLinkMetrics> metrics = new Mock<IGitHubLinkMetrics>();
            ILogger<LinkGitHubAccountCommandHandler> logger = Mock.Of<ILogger<LinkGitHubAccountCommandHandler>>();

            LinkGitHubAccountCommandHandler handler = new LinkGitHubAccountCommandHandler(
                externalLoginRepository.Object,
                accountLinkRepository.Object,
                credentialStore.Object,
                gitHubOAuthClient.Object,
                dateTimeProvider.Object,
                publisher.Object,
                metrics.Object,
                logger);

            Func<Task> action = async () => await handler.Handle(command, CancellationToken.None);

            await action.Should().ThrowAsync<InvalidOperationException>();
            metrics.Verify(m => m.RecordLinkFailure(userId, It.IsAny<string>()), Times.Once);
        }
    }
}
