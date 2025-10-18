#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
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
            Dictionary<string, long> counterValues = new Dictionary<string, long>();
            using MeterListener listener = CreateListener(meter, counterValues);

            DateTimeOffset initialTime = DateTimeOffset.UtcNow;
            clockMock.Setup(clock => clock.UtcNow).Returns(initialTime);
            Guid userId = Guid.NewGuid();
            UserExternalLogin login = new UserExternalLogin(userId, "GitHub", "node", "oldAccess", "oldRefresh", initialTime.AddMinutes(10));
            repositoryMock.Setup(repository => repository.GetAsync(userId, "GitHub", It.IsAny<CancellationToken>()))
                .ReturnsAsync(login);

            GitHubOAuthTokenResponse tokenResponse = new GitHubOAuthTokenResponse("newAccess", "newRefresh", 7200, "bearer", "repo workflow read:user", "node");
            gitHubOAuthClientMock.Setup(client => client.RefreshTokenAsync(It.IsAny<GitHubTokenRefreshRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(tokenResponse);

            validatorMock.Setup(validator => validator.ValidateAsync(It.IsAny<RefreshGitHubTokenCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ValidationResult());

            RefreshGitHubTokenCommandHandler handler = new RefreshGitHubTokenCommandHandler(
                gitHubOAuthClientMock.Object,
                repositoryMock.Object,
                clockMock.Object,
                validatorMock.Object,
                loggerMock.Object,
                meter);

            RefreshGitHubTokenCommand command = new RefreshGitHubTokenCommand(userId, "state", "https://example.com/callback");

            RefreshGitHubTokenResultDto result = await handler.Handle(command, CancellationToken.None);

            listener.Dispose();

            result.UserId.Should().Be(userId);
            result.CanClone.Should().BeTrue();
            repositoryMock.Verify(repository => repository.UpdateAsync(login, It.IsAny<CancellationToken>()), Times.Once);
            login.AccessToken.Should().Be("newAccess");
            login.RefreshToken.Should().Be("newRefresh");
            counterValues.GetValueOrDefault("github.oauth.refresh.success.count").Should().Be(1);
            counterValues.ContainsKey("github.oauth.refresh.expired.count").Should().BeFalse();
        }

        [Fact]
        public async Task Handle_ShouldRaiseExpiredAndSuccessCounters_WhenTokenExpired()
        {
            Mock<IGitHubOAuthClient> gitHubOAuthClientMock = new Mock<IGitHubOAuthClient>();
            Mock<IUserExternalLoginRepository> repositoryMock = new Mock<IUserExternalLoginRepository>();
            Mock<ISystemClock> clockMock = new Mock<ISystemClock>();
            Mock<IValidator<RefreshGitHubTokenCommand>> validatorMock = new Mock<IValidator<RefreshGitHubTokenCommand>>();
            Mock<Microsoft.Extensions.Logging.ILogger<RefreshGitHubTokenCommandHandler>> loggerMock = new Mock<Microsoft.Extensions.Logging.ILogger<RefreshGitHubTokenCommandHandler>>();
            Meter meter = new Meter("TestMeter.RefreshExpired");
            Dictionary<string, long> counterValues = new Dictionary<string, long>();
            using MeterListener listener = CreateListener(meter, counterValues);

            DateTimeOffset issuedAt = DateTimeOffset.UtcNow;
            Guid userId = Guid.NewGuid();
            UserExternalLogin login = new UserExternalLogin(userId, "GitHub", "node", "oldAccess", "oldRefresh", issuedAt.AddMinutes(2));
            DateTimeOffset refreshTime = issuedAt.AddMinutes(10);
            clockMock.Setup(clock => clock.UtcNow).Returns(refreshTime);
            repositoryMock.Setup(repository => repository.GetAsync(userId, "GitHub", It.IsAny<CancellationToken>()))
                .ReturnsAsync(login);

            GitHubOAuthTokenResponse tokenResponse = new GitHubOAuthTokenResponse("access", "refresh", 3600, "bearer", "repo workflow", "node");
            gitHubOAuthClientMock.Setup(client => client.RefreshTokenAsync(It.IsAny<GitHubTokenRefreshRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(tokenResponse);

            validatorMock.Setup(validator => validator.ValidateAsync(It.IsAny<RefreshGitHubTokenCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ValidationResult());

            RefreshGitHubTokenCommandHandler handler = new RefreshGitHubTokenCommandHandler(
                gitHubOAuthClientMock.Object,
                repositoryMock.Object,
                clockMock.Object,
                validatorMock.Object,
                loggerMock.Object,
                meter);

            RefreshGitHubTokenCommand command = new RefreshGitHubTokenCommand(userId, "state", "https://example.com/callback");

            RefreshGitHubTokenResultDto result = await handler.Handle(command, CancellationToken.None);

            listener.Dispose();

            result.WasExpired.Should().BeTrue();
            counterValues.GetValueOrDefault("github.oauth.refresh.expired.count").Should().Be(1);
            counterValues.GetValueOrDefault("github.oauth.refresh.success.count").Should().Be(1);
        }

        [Fact]
        public async Task Handle_ShouldIncrementFailureCounter_WhenLoginMissing()
        {
            Mock<IGitHubOAuthClient> gitHubOAuthClientMock = new Mock<IGitHubOAuthClient>();
            Mock<IUserExternalLoginRepository> repositoryMock = new Mock<IUserExternalLoginRepository>();
            Mock<ISystemClock> clockMock = new Mock<ISystemClock>();
            Mock<IValidator<RefreshGitHubTokenCommand>> validatorMock = new Mock<IValidator<RefreshGitHubTokenCommand>>();
            Mock<Microsoft.Extensions.Logging.ILogger<RefreshGitHubTokenCommandHandler>> loggerMock = new Mock<Microsoft.Extensions.Logging.ILogger<RefreshGitHubTokenCommandHandler>>();
            Meter meter = new Meter("TestMeter.RefreshFailure");
            Dictionary<string, long> counterValues = new Dictionary<string, long>();
            using MeterListener listener = CreateListener(meter, counterValues);

            Guid userId = Guid.NewGuid();
            clockMock.Setup(clock => clock.UtcNow).Returns(DateTimeOffset.UtcNow);
            repositoryMock.Setup(repository => repository.GetAsync(userId, "GitHub", It.IsAny<CancellationToken>()))
                .ReturnsAsync((UserExternalLogin?)null);

            validatorMock.Setup(validator => validator.ValidateAsync(It.IsAny<RefreshGitHubTokenCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ValidationResult());

            RefreshGitHubTokenCommandHandler handler = new RefreshGitHubTokenCommandHandler(
                gitHubOAuthClientMock.Object,
                repositoryMock.Object,
                clockMock.Object,
                validatorMock.Object,
                loggerMock.Object,
                meter);

            RefreshGitHubTokenCommand command = new RefreshGitHubTokenCommand(userId, "state", "https://example.com/callback");

            await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(command, CancellationToken.None));

            listener.Dispose();

            counterValues.GetValueOrDefault("github.oauth.refresh.failure.count").Should().Be(1);
            counterValues.ContainsKey("github.oauth.refresh.success.count").Should().BeFalse();
        }

        [Fact]
        public async Task Handle_ShouldThrowWhenRefreshTokenUnavailable()
        {
            Mock<IGitHubOAuthClient> gitHubOAuthClientMock = new Mock<IGitHubOAuthClient>();
            Mock<IUserExternalLoginRepository> repositoryMock = new Mock<IUserExternalLoginRepository>();
            Mock<ISystemClock> clockMock = new Mock<ISystemClock>();
            Mock<IValidator<RefreshGitHubTokenCommand>> validatorMock = new Mock<IValidator<RefreshGitHubTokenCommand>>();
            Mock<Microsoft.Extensions.Logging.ILogger<RefreshGitHubTokenCommandHandler>> loggerMock = new Mock<Microsoft.Extensions.Logging.ILogger<RefreshGitHubTokenCommandHandler>>();
            Meter meter = new Meter("TestMeter.RefreshMissing");
            Dictionary<string, long> counterValues = new Dictionary<string, long>();
            using MeterListener listener = CreateListener(meter, counterValues);

            DateTimeOffset currentTime = DateTimeOffset.UtcNow;
            Guid userId = Guid.NewGuid();
            UserExternalLogin login = new UserExternalLogin(userId, "GitHub", "node", "oldAccess", null, currentTime.AddMinutes(10));
            clockMock.Setup(clock => clock.UtcNow).Returns(currentTime);
            repositoryMock.Setup(repository => repository.GetAsync(userId, "GitHub", It.IsAny<CancellationToken>()))
                .ReturnsAsync(login);

            validatorMock.Setup(validator => validator.ValidateAsync(It.IsAny<RefreshGitHubTokenCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ValidationResult());

            RefreshGitHubTokenCommandHandler handler = new RefreshGitHubTokenCommandHandler(
                gitHubOAuthClientMock.Object,
                repositoryMock.Object,
                clockMock.Object,
                validatorMock.Object,
                loggerMock.Object,
                meter);

            RefreshGitHubTokenCommand command = new RefreshGitHubTokenCommand(userId, "state", "https://example.com/callback");

            await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(command, CancellationToken.None));

            listener.Dispose();

            counterValues.GetValueOrDefault("github.oauth.refresh.failure.count").Should().Be(1);
            gitHubOAuthClientMock.Verify(client => client.RefreshTokenAsync(It.IsAny<GitHubTokenRefreshRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        private static MeterListener CreateListener(Meter meter, Dictionary<string, long> counterValues)
        {
            MeterListener listener = new MeterListener();
            listener.InstrumentPublished = (instrument, meterListener) =>
            {
                if (instrument.Meter == meter)
                {
                    meterListener.EnableMeasurementEvents(instrument);
                }
            };
            listener.SetMeasurementEventCallback<int>((instrument, measurement, tags, state) =>
            {
                long current;
                if (!counterValues.TryGetValue(instrument.Name, out current))
                {
                    current = 0;
                }

                counterValues[instrument.Name] = current + measurement;
            });
            listener.Start();
            return listener;
        }
    }
}
