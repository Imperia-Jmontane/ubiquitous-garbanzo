#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.Logging;
using Moq;
using MyApp.Application.Abstractions;
using MyApp.Application.GitHubOAuth.Commands.StartGitHubOAuth;
using MyApp.Application.GitHubOAuth.Configuration;
using MyApp.Application.GitHubOAuth.DTOs;
using MyApp.Domain.Identity;
using Xunit;

namespace MyApp.Tests.GitHubOAuth
{
    public sealed class StartGitHubOAuthCommandHandlerTests
    {
        [Fact]
        public async Task Handle_ShouldCreateStateAndReturnAuthorizationData()
        {
            Mock<IGitHubOAuthStateRepository> stateRepositoryMock = new Mock<IGitHubOAuthStateRepository>();
            Mock<ISystemClock> clockMock = new Mock<ISystemClock>();
            Mock<IValidator<StartGitHubOAuthCommand>> validatorMock = new Mock<IValidator<StartGitHubOAuthCommand>>();
            Mock<ILogger<StartGitHubOAuthCommandHandler>> loggerMock = new Mock<ILogger<StartGitHubOAuthCommandHandler>>();

            GitHubOAuthSettings settings = new GitHubOAuthSettings(
                "client-id",
                "https://github.com/login/oauth/authorize",
                "https://github.com/login/oauth/access_token",
                "https://api.github.com/user",
                "/signin-github",
                new[] { "repo", "workflow", "read:user" },
                true);
            Mock<IGitHubOAuthSettingsProvider> settingsProviderMock = new Mock<IGitHubOAuthSettingsProvider>();
            settingsProviderMock.Setup(provider => provider.GetSettingsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(settings);

            DateTimeOffset now = DateTimeOffset.UtcNow;
            clockMock.SetupGet(clock => clock.UtcNow).Returns(now);
            validatorMock.Setup(validator => validator.ValidateAsync(It.IsAny<StartGitHubOAuthCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ValidationResult());
            stateRepositoryMock.Setup(repository => repository.RemoveExpiredAsync(It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            GitHubOAuthState? capturedState = null;
            stateRepositoryMock.Setup(repository => repository.AddAsync(It.IsAny<GitHubOAuthState>(), It.IsAny<CancellationToken>()))
                .Callback<GitHubOAuthState, CancellationToken>((state, token) => capturedState = state)
                .Returns(Task.CompletedTask);

            StartGitHubOAuthCommandHandler handler = new StartGitHubOAuthCommandHandler(
                stateRepositoryMock.Object,
                clockMock.Object,
                validatorMock.Object,
                loggerMock.Object,
                settingsProviderMock.Object);

            Guid userId = Guid.NewGuid();
            StartGitHubOAuthCommand command = new StartGitHubOAuthCommand(userId, "https://localhost/signin-github");

            StartGitHubOAuthResultDto result = await handler.Handle(command, CancellationToken.None);

            Assert.NotNull(capturedState);
            Assert.Equal(userId, result.UserId);
            Assert.Equal(result.State, capturedState!.State);
            Assert.Contains("github.com/login/oauth/authorize", result.AuthorizationUrl, StringComparison.OrdinalIgnoreCase);
            Assert.True(result.CanClone);
            Assert.True(result.ExpiresAt > now);
        }

        [Fact]
        public async Task Handle_ShouldReturnCanCloneFalse_WhenMandatoryScopesMissing()
        {
            Mock<IGitHubOAuthStateRepository> stateRepositoryMock = new Mock<IGitHubOAuthStateRepository>();
            Mock<ISystemClock> clockMock = new Mock<ISystemClock>();
            Mock<IValidator<StartGitHubOAuthCommand>> validatorMock = new Mock<IValidator<StartGitHubOAuthCommand>>();
            Mock<ILogger<StartGitHubOAuthCommandHandler>> loggerMock = new Mock<ILogger<StartGitHubOAuthCommandHandler>>();

            GitHubOAuthSettings settings = new GitHubOAuthSettings(
                "client-id",
                "https://github.com/login/oauth/authorize",
                "https://github.com/login/oauth/access_token",
                "https://api.github.com/user",
                "/signin-github",
                new[] { "notifications" },
                true);
            Mock<IGitHubOAuthSettingsProvider> settingsProviderMock = new Mock<IGitHubOAuthSettingsProvider>();
            settingsProviderMock.Setup(provider => provider.GetSettingsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(settings);

            DateTimeOffset now = DateTimeOffset.UtcNow;
            clockMock.SetupGet(clock => clock.UtcNow).Returns(now);
            validatorMock.Setup(validator => validator.ValidateAsync(It.IsAny<StartGitHubOAuthCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ValidationResult());
            stateRepositoryMock.Setup(repository => repository.RemoveExpiredAsync(It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            stateRepositoryMock.Setup(repository => repository.AddAsync(It.IsAny<GitHubOAuthState>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            StartGitHubOAuthCommandHandler handler = new StartGitHubOAuthCommandHandler(
                stateRepositoryMock.Object,
                clockMock.Object,
                validatorMock.Object,
                loggerMock.Object,
                settingsProviderMock.Object);

            StartGitHubOAuthCommand command = new StartGitHubOAuthCommand(Guid.NewGuid(), "https://localhost/signin-github");

            StartGitHubOAuthResultDto result = await handler.Handle(command, CancellationToken.None);

            Assert.False(result.CanClone);
            Assert.Contains("notifications", result.Scopes);
        }

        [Fact]
        public async Task Handle_ShouldThrow_WhenSettingsAreNotConfigured()
        {
            Mock<IGitHubOAuthStateRepository> stateRepositoryMock = new Mock<IGitHubOAuthStateRepository>();
            Mock<ISystemClock> clockMock = new Mock<ISystemClock>();
            Mock<IValidator<StartGitHubOAuthCommand>> validatorMock = new Mock<IValidator<StartGitHubOAuthCommand>>();
            Mock<ILogger<StartGitHubOAuthCommandHandler>> loggerMock = new Mock<ILogger<StartGitHubOAuthCommandHandler>>();

            GitHubOAuthSettings settings = new GitHubOAuthSettings(
                string.Empty,
                "https://github.com/login/oauth/authorize",
                "https://github.com/login/oauth/access_token",
                "https://api.github.com/user",
                "/signin-github",
                Array.Empty<string>(),
                false);
            Mock<IGitHubOAuthSettingsProvider> settingsProviderMock = new Mock<IGitHubOAuthSettingsProvider>();
            settingsProviderMock.Setup(provider => provider.GetSettingsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(settings);

            validatorMock.Setup(validator => validator.ValidateAsync(It.IsAny<StartGitHubOAuthCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ValidationResult());
            stateRepositoryMock.Setup(repository => repository.RemoveExpiredAsync(It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            StartGitHubOAuthCommandHandler handler = new StartGitHubOAuthCommandHandler(
                stateRepositoryMock.Object,
                clockMock.Object,
                validatorMock.Object,
                loggerMock.Object,
                settingsProviderMock.Object);

            StartGitHubOAuthCommand command = new StartGitHubOAuthCommand(Guid.NewGuid(), "https://localhost/signin-github");

            await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(command, CancellationToken.None));
        }
    }
}
