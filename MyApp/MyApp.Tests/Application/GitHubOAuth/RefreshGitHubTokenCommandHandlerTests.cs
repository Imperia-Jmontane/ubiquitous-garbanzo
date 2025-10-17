using System;
using System.Diagnostics.Metrics;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using FluentValidation.Results;
using FluentAssertions;
using Moq;
using MyApp.Application.Abstractions;
using MyApp.Application.GitHubOAuth.Commands.RefreshGitHubToken;
using MyApp.Application.GitHubOAuth.DTOs;
using MyApp.Application.GitHubOAuth.Models;
using MyApp.Domain.Identity;
using Xunit;

namespace MyApp.Tests.Application.GitHubOAuth
{
    public sealed class RefreshGitHubTokenCommandHandlerTests
    {
        [Fact]
        public async Task Handle_ShouldUpdateTokens_WhenLoginExists()
        {
            Mock<IGitHubOAuthClient> gitHubOAuthClientMock = new Mock<IGitHubOAuthClient>();
            Mock<IUserExternalLoginRepository> repositoryMock = new Mock<IUserExternalLoginRepository>();
            Mock<ISystemClock> clockMock = new Mock<ISystemClock>();
            Mock<IValidator<RefreshGitHubTokenCommand>> validatorMock = new Mock<IValidator<RefreshGitHubTokenCommand>>();
            Mock<Microsoft.Extensions.Logging.ILogger<RefreshGitHubTokenCommandHandler>> loggerMock = new Mock<Microsoft.Extensions.Logging.ILogger<RefreshGitHubTokenCommandHandler>>();
            Meter meter = new Meter("TestMeter.Refresh");

            Guid userId = Guid.NewGuid();
            DateTimeOffset now = DateTimeOffset.UtcNow;
            clockMock.Setup(clock => clock.UtcNow).Returns(now);

            UserExternalLogin login = new UserExternalLogin(userId, "GitHub", "node", "oldAccess", "oldRefresh", now.AddMinutes(10));
            repositoryMock.Setup(repository => repository.GetAsync(userId, "GitHub", It.IsAny<CancellationToken>())).ReturnsAsync(login);

            GitHubOAuthTokenResponse tokenResponse = new GitHubOAuthTokenResponse("newAccess", "newRefresh", 7200, "bearer", "repo", "node");
            gitHubOAuthClientMock.Setup(client => client.RefreshTokenAsync(It.IsAny<GitHubTokenRefreshRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(tokenResponse);

            validatorMock.Setup(validator => validator.ValidateAsync(It.IsAny<RefreshGitHubTokenCommand>(), It.IsAny<CancellationToken>())).ReturnsAsync(new ValidationResult());

            RefreshGitHubTokenCommandHandler handler = new RefreshGitHubTokenCommandHandler(
                gitHubOAuthClientMock.Object,
                repositoryMock.Object,
                clockMock.Object,
                validatorMock.Object,
                loggerMock.Object,
                meter);

            RefreshGitHubTokenCommand command = new RefreshGitHubTokenCommand(userId, "state", "https://example.com/callback");

            RefreshGitHubTokenResultDto result = await handler.Handle(command, CancellationToken.None);

            result.UserId.Should().Be(userId);
            repositoryMock.Verify(repository => repository.UpdateAsync(login, It.IsAny<CancellationToken>()), Times.Once);
            login.AccessToken.Should().Be("newAccess");
            login.RefreshToken.Should().Be("newRefresh");
        }
    }
}
