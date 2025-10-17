using System;
using System.Diagnostics.Metrics;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using FluentValidation.Results;
using FluentAssertions;
using Moq;
using MyApp.Application.Abstractions;
using MyApp.Application.GitHubOAuth.Commands.LinkGitHubAccount;
using MyApp.Application.GitHubOAuth.DTOs;
using MyApp.Application.GitHubOAuth.Models;
using MyApp.Domain.Identity;
using Xunit;

namespace MyApp.Tests.Application.GitHubOAuth
{
    public sealed class LinkGitHubAccountCommandHandlerTests
    {
        [Fact]
        public async Task Handle_ShouldCreateNewLogin_WhenNoExistingRecord()
        {
            Mock<IGitHubOAuthClient> gitHubOAuthClientMock = new Mock<IGitHubOAuthClient>();
            Mock<IUserExternalLoginRepository> repositoryMock = new Mock<IUserExternalLoginRepository>();
            Mock<ISystemClock> clockMock = new Mock<ISystemClock>();
            Mock<IValidator<LinkGitHubAccountCommand>> validatorMock = new Mock<IValidator<LinkGitHubAccountCommand>>();
            Mock<Microsoft.Extensions.Logging.ILogger<LinkGitHubAccountCommandHandler>> loggerMock = new Mock<Microsoft.Extensions.Logging.ILogger<LinkGitHubAccountCommandHandler>>();
            Meter meter = new Meter("TestMeter");

            Guid userId = Guid.NewGuid();
            DateTimeOffset now = DateTimeOffset.UtcNow;
            clockMock.Setup(clock => clock.UtcNow).Returns(now);

            GitHubOAuthTokenResponse tokenResponse = new GitHubOAuthTokenResponse("access", "refresh", 3600, "bearer", "repo read:user", "node123");
            gitHubOAuthClientMock
                .Setup(client => client.ExchangeCodeAsync(It.IsAny<GitHubCodeExchangeRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(tokenResponse);

            validatorMock
                .Setup(validator => validator.ValidateAsync(It.IsAny<LinkGitHubAccountCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ValidationResult());

            LinkGitHubAccountCommandHandler handler = new LinkGitHubAccountCommandHandler(
                gitHubOAuthClientMock.Object,
                repositoryMock.Object,
                clockMock.Object,
                validatorMock.Object,
                loggerMock.Object,
                meter);

            LinkGitHubAccountCommand command = new LinkGitHubAccountCommand(userId, "code", "state", "https://example.com/callback");

            LinkGitHubAccountResultDto result = await handler.Handle(command, CancellationToken.None);

            result.UserId.Should().Be(userId);
            result.IsNewConnection.Should().BeTrue();
            result.Scopes.Should().Contain("repo");
            result.Scopes.Should().Contain("read:user");
            repositoryMock.Verify(repository => repository.AddAsync(It.IsAny<UserExternalLogin>(), It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
