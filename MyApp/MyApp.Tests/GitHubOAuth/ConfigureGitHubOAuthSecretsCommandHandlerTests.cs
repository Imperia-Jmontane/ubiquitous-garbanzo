#nullable enable
using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using MyApp.Application.Abstractions;
using MyApp.Application.Configuration;
using MyApp.Application.GitHubOAuth.Commands.ConfigureGitHubOAuthSecrets;
using MyApp.Application.GitHubOAuth.DTOs;
using MyApp.Domain.Observability;
using Xunit;

namespace MyApp.Tests.GitHubOAuth
{
    public sealed class ConfigureGitHubOAuthSecretsCommandHandlerTests
    {
        [Fact]
        public async Task Handle_ShouldPersistSecretsAndAddAuditTrail()
        {
            Mock<IWritableSecretStore> secretStoreMock = new Mock<IWritableSecretStore>();
            Mock<IAuditTrailRepository> auditRepositoryMock = new Mock<IAuditTrailRepository>();
            Mock<ISystemClock> clockMock = new Mock<ISystemClock>();
            ConfigureGitHubOAuthSecretsCommandValidator validator = new ConfigureGitHubOAuthSecretsCommandValidator();
            ILogger<ConfigureGitHubOAuthSecretsCommandHandler> logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<ConfigureGitHubOAuthSecretsCommandHandler>.Instance;

            DateTimeOffset now = DateTimeOffset.UtcNow;
            clockMock.SetupGet(clock => clock.UtcNow).Returns(now);

            Guid auditUserId = Guid.NewGuid();
            BootstrapOptions options = new BootstrapOptions
            {
                SetupPassword = "bootstrap-secret",
                AuditUserId = auditUserId
            };
            IOptions<BootstrapOptions> optionsWrapper = Options.Create(options);

            ConfigureGitHubOAuthSecretsCommandHandler handler = new ConfigureGitHubOAuthSecretsCommandHandler(
                secretStoreMock.Object,
                auditRepositoryMock.Object,
                clockMock.Object,
                validator,
                logger,
                optionsWrapper);

            ConfigureGitHubOAuthSecretsCommand command = new ConfigureGitHubOAuthSecretsCommand("client-12345", "secret-value-with-sufficient-length", "bootstrap-secret");

            ConfigureGitHubOAuthSecretsResultDto result = await handler.Handle(command, CancellationToken.None);

            result.Configured.Should().BeTrue();
            secretStoreMock.Verify(store => store.SetSecretAsync("GitHubClientId", "client-12345", It.IsAny<CancellationToken>()), Times.Once);
            secretStoreMock.Verify(store => store.SetSecretAsync("GitHubClientSecret", "secret-value-with-sufficient-length", It.IsAny<CancellationToken>()), Times.Once);

            auditRepositoryMock.Verify(repository => repository.AddAsync(It.Is<AuditTrailEntry>(entry =>
                entry.UserId == auditUserId &&
                entry.EventType == "GitHubOAuthSecretsConfigured" &&
                entry.Provider == "GitHub" &&
                ExtractClientIdSuffix(entry.Payload) == "2345"),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Handle_ShouldThrow_WhenSetupPasswordIsInvalid()
        {
            Mock<IWritableSecretStore> secretStoreMock = new Mock<IWritableSecretStore>();
            Mock<IAuditTrailRepository> auditRepositoryMock = new Mock<IAuditTrailRepository>();
            Mock<ISystemClock> clockMock = new Mock<ISystemClock>();
            ConfigureGitHubOAuthSecretsCommandValidator validator = new ConfigureGitHubOAuthSecretsCommandValidator();
            ILogger<ConfigureGitHubOAuthSecretsCommandHandler> logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<ConfigureGitHubOAuthSecretsCommandHandler>.Instance;

            BootstrapOptions options = new BootstrapOptions
            {
                SetupPassword = "expected-secret",
                AuditUserId = Guid.NewGuid()
            };
            IOptions<BootstrapOptions> optionsWrapper = Options.Create(options);

            ConfigureGitHubOAuthSecretsCommandHandler handler = new ConfigureGitHubOAuthSecretsCommandHandler(
                secretStoreMock.Object,
                auditRepositoryMock.Object,
                clockMock.Object,
                validator,
                logger,
                optionsWrapper);

            ConfigureGitHubOAuthSecretsCommand command = new ConfigureGitHubOAuthSecretsCommand("client-12345", "secret-value-with-sufficient-length", "wrong-secret");

            await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(command, CancellationToken.None));

            secretStoreMock.Verify(store => store.SetSecretAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
            auditRepositoryMock.Verify(repository => repository.AddAsync(It.IsAny<AuditTrailEntry>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        private static string ExtractClientIdSuffix(string payload)
        {
            using JsonDocument document = JsonDocument.Parse(payload);
            JsonElement root = document.RootElement;
            if (root.TryGetProperty("clientIdSuffix", out JsonElement suffixElement) && suffixElement.GetString() != null)
            {
                return suffixElement.GetString()!;
            }

            return string.Empty;
        }
    }
}
