#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using MyApp.Application.Abstractions;
using MyApp.Application.GitHubOAuth.Commands.LinkGitHubAccount;
using MyApp.Application.GitHubOAuth.DTOs;
using MyApp.Application.GitHubOAuth.Events;
using MyApp.Application.GitHubOAuth.Exceptions;
using MyApp.Application.GitHubOAuth.Models;
using MyApp.Domain.Identity;
using Xunit;

namespace MyApp.Tests.GitHubOAuth
{
    public sealed class LinkGitHubAccountCommandHandlerTests
    {
        [Fact]
        public async Task Handle_ShouldPersistTokensPublishEventAndEmitMetrics()
        {
            Mock<IGitHubOAuthClient> gitHubOAuthClientMock = new Mock<IGitHubOAuthClient>();
            Mock<IUserExternalLoginRepository> loginRepositoryMock = new Mock<IUserExternalLoginRepository>();
            Mock<ISystemClock> clockMock = new Mock<ISystemClock>();
            Mock<IValidator<LinkGitHubAccountCommand>> validatorMock = new Mock<IValidator<LinkGitHubAccountCommand>>();
            Mock<ILogger<LinkGitHubAccountCommandHandler>> loggerMock = new Mock<ILogger<LinkGitHubAccountCommandHandler>>();
            Mock<IGitHubOAuthStateRepository> stateRepositoryMock = new Mock<IGitHubOAuthStateRepository>();
            Mock<IPublisher> publisherMock = new Mock<IPublisher>();

            validatorMock.Setup(validator => validator.ValidateAsync(It.IsAny<LinkGitHubAccountCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ValidationResult());

            DateTimeOffset now = DateTimeOffset.UtcNow;
            clockMock.SetupGet(clock => clock.UtcNow).Returns(now);

            Guid userId = Guid.NewGuid();
            string state = Guid.NewGuid().ToString("N");
            GitHubOAuthState storedState = new GitHubOAuthState(userId, state, "https://localhost/signin-github", now.AddMinutes(5));

            stateRepositoryMock.Setup(repository => repository.RemoveExpiredAsync(It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            stateRepositoryMock.Setup(repository => repository.GetAsync(state, It.IsAny<CancellationToken>()))
                .ReturnsAsync(storedState);
            stateRepositoryMock.Setup(repository => repository.RemoveAsync(storedState, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask)
                .Verifiable();

            loginRepositoryMock.Setup(repository => repository.GetAsync(userId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((UserExternalLogin?)null);
            loginRepositoryMock.Setup(repository => repository.AddAsync(It.IsAny<UserExternalLogin>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask)
                .Verifiable();

            GitHubOAuthTokenResponse tokenResponse = new GitHubOAuthTokenResponse("access-token", "refresh-token", 3600, "bearer", "repo workflow read:user", "12345");
            gitHubOAuthClientMock.Setup(client => client.ExchangeCodeAsync(It.IsAny<GitHubCodeExchangeRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(tokenResponse);

            GitHubAccountLinkedEvent? publishedEvent = null;
            publisherMock.Setup(publisher => publisher.Publish(It.IsAny<GitHubAccountLinkedEvent>(), It.IsAny<CancellationToken>()))
                .Callback<INotification, CancellationToken>((notification, token) => publishedEvent = notification as GitHubAccountLinkedEvent)
                .Returns(Task.CompletedTask);

            Meter meter = new Meter("test.github.oauth");
            Dictionary<string, long> counterValues = new Dictionary<string, long>();
            using MeterListener listener = CreateListener(meter, counterValues);

            LinkGitHubAccountCommandHandler handler = new LinkGitHubAccountCommandHandler(
                gitHubOAuthClientMock.Object,
                loginRepositoryMock.Object,
                clockMock.Object,
                validatorMock.Object,
                loggerMock.Object,
                meter,
                stateRepositoryMock.Object,
                publisherMock.Object);

            LinkGitHubAccountCommand command = new LinkGitHubAccountCommand(userId, "auth-code", state);

            LinkGitHubAccountResultDto result = await handler.Handle(command, CancellationToken.None);

            listener.Dispose();

            loginRepositoryMock.Verify();
            stateRepositoryMock.Verify();
            Assert.True(result.IsNewConnection);
            Assert.True(result.CanClone);
            Assert.NotNull(publishedEvent);
            Assert.Equal(userId, publishedEvent!.UserId);
            Assert.True(publishedEvent.CanClone);
            Assert.Equal(1, counterValues.GetValueOrDefault("github.oauth.link.success.count"));
            Assert.False(counterValues.ContainsKey("github.oauth.link.failure.count"));
        }

        [Fact]
        public async Task Handle_ShouldIncrementFailureCounterWhenStateMissing()
        {
            Mock<IGitHubOAuthClient> gitHubOAuthClientMock = new Mock<IGitHubOAuthClient>();
            Mock<IUserExternalLoginRepository> loginRepositoryMock = new Mock<IUserExternalLoginRepository>();
            Mock<ISystemClock> clockMock = new Mock<ISystemClock>();
            Mock<IValidator<LinkGitHubAccountCommand>> validatorMock = new Mock<IValidator<LinkGitHubAccountCommand>>();
            Mock<ILogger<LinkGitHubAccountCommandHandler>> loggerMock = new Mock<ILogger<LinkGitHubAccountCommandHandler>>();
            Mock<IGitHubOAuthStateRepository> stateRepositoryMock = new Mock<IGitHubOAuthStateRepository>();
            Mock<IPublisher> publisherMock = new Mock<IPublisher>();

            validatorMock.Setup(validator => validator.ValidateAsync(It.IsAny<LinkGitHubAccountCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ValidationResult());

            stateRepositoryMock.Setup(repository => repository.RemoveExpiredAsync(It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            stateRepositoryMock.Setup(repository => repository.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((GitHubOAuthState?)null);

            Meter meter = new Meter("test.github.oauth.failures");
            Dictionary<string, long> counterValues = new Dictionary<string, long>();
            using MeterListener listener = CreateListener(meter, counterValues);

            LinkGitHubAccountCommandHandler handler = new LinkGitHubAccountCommandHandler(
                gitHubOAuthClientMock.Object,
                loginRepositoryMock.Object,
                clockMock.Object,
                validatorMock.Object,
                loggerMock.Object,
                meter,
                stateRepositoryMock.Object,
                publisherMock.Object);

            LinkGitHubAccountCommand command = new LinkGitHubAccountCommand(Guid.NewGuid(), "code", "state");

            await Assert.ThrowsAsync<InvalidGitHubOAuthStateException>(() => handler.Handle(command, CancellationToken.None));

            listener.Dispose();

            Assert.Equal(1, counterValues.GetValueOrDefault("github.oauth.link.failure.count"));
            Assert.False(counterValues.ContainsKey("github.oauth.link.success.count"));
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
