#nullable enable
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.Logging;
using Moq;
using MyApp.Application.Abstractions;
using MyApp.Application.GitHubPersonalAccessToken;
using MyApp.Application.GitHubPersonalAccessToken.Commands.ConfigureGitHubPersonalAccessToken;
using MyApp.Application.GitHubPersonalAccessToken.DTOs;
using MyApp.Application.GitHubPersonalAccessToken.Models;
using Xunit;

namespace MyApp.Tests.Application.GitHubPersonalAccessToken
{
    public sealed class ConfigureGitHubPersonalAccessTokenCommandHandlerTests
    {
        [Fact]
        public async Task Handle_ShouldThrowValidationException_WhenTokenRejected()
        {
            Mock<IWritableSecretStore> storeMock = new Mock<IWritableSecretStore>();
            Mock<IValidator<ConfigureGitHubPersonalAccessTokenCommand>> validatorMock = new Mock<IValidator<ConfigureGitHubPersonalAccessTokenCommand>>();
            validatorMock
                .Setup(validator => validator.ValidateAsync(It.IsAny<ConfigureGitHubPersonalAccessTokenCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ValidationResult());

            GitHubPersonalAccessTokenInspectionResult inspection = new GitHubPersonalAccessTokenInspectionResult(
                false,
                false,
                false,
                false,
                null,
                new List<string>(),
                new List<string>(GitHubPersonalAccessTokenRequirements.RequiredScopes),
                new List<string>(),
                "GitHub rechazó el token. Verifica que no haya expirado y que lo copiaste completo.");

            Mock<IGitHubPersonalAccessTokenInspector> inspectorMock = new Mock<IGitHubPersonalAccessTokenInspector>();
            inspectorMock
                .Setup(inspector => inspector.InspectAsync("github_pat_token", GitHubPersonalAccessTokenRequirements.RequiredScopes, It.IsAny<CancellationToken>()))
                .ReturnsAsync(inspection);

            Mock<ILogger<ConfigureGitHubPersonalAccessTokenCommandHandler>> loggerMock = new Mock<ILogger<ConfigureGitHubPersonalAccessTokenCommandHandler>>();

            ConfigureGitHubPersonalAccessTokenCommandHandler handler = new ConfigureGitHubPersonalAccessTokenCommandHandler(
                storeMock.Object,
                validatorMock.Object,
                loggerMock.Object,
                inspectorMock.Object);

            ConfigureGitHubPersonalAccessTokenCommand command = new ConfigureGitHubPersonalAccessTokenCommand("github_pat_token");

            await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(command, CancellationToken.None));
            storeMock.Verify(store => store.SetSecretAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Handle_ShouldThrowValidationException_WhenPermissionsMissing()
        {
            Mock<IWritableSecretStore> storeMock = new Mock<IWritableSecretStore>();
            Mock<IValidator<ConfigureGitHubPersonalAccessTokenCommand>> validatorMock = new Mock<IValidator<ConfigureGitHubPersonalAccessTokenCommand>>();
            validatorMock
                .Setup(validator => validator.ValidateAsync(It.IsAny<ConfigureGitHubPersonalAccessTokenCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ValidationResult());

            GitHubPersonalAccessTokenInspectionResult inspection = new GitHubPersonalAccessTokenInspectionResult(
                true,
                false,
                false,
                false,
                "octocat",
                new List<string> { "read:user" },
                new List<string> { "repo" },
                new List<string>(),
                null);

            Mock<IGitHubPersonalAccessTokenInspector> inspectorMock = new Mock<IGitHubPersonalAccessTokenInspector>();
            inspectorMock
                .Setup(inspector => inspector.InspectAsync("github_pat_token", GitHubPersonalAccessTokenRequirements.RequiredScopes, It.IsAny<CancellationToken>()))
                .ReturnsAsync(inspection);

            Mock<ILogger<ConfigureGitHubPersonalAccessTokenCommandHandler>> loggerMock = new Mock<ILogger<ConfigureGitHubPersonalAccessTokenCommandHandler>>();

            ConfigureGitHubPersonalAccessTokenCommandHandler handler = new ConfigureGitHubPersonalAccessTokenCommandHandler(
                storeMock.Object,
                validatorMock.Object,
                loggerMock.Object,
                inspectorMock.Object);

            ConfigureGitHubPersonalAccessTokenCommand command = new ConfigureGitHubPersonalAccessTokenCommand("github_pat_token");

            await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(command, CancellationToken.None));
            storeMock.Verify(store => store.SetSecretAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Handle_ShouldStoreToken_WhenInspectionSuccessful()
        {
            Mock<IWritableSecretStore> storeMock = new Mock<IWritableSecretStore>();
            storeMock
                .Setup(store => store.SetSecretAsync("GitHubPersonalAccessToken", "github_pat_token", It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask)
                .Verifiable();

            Mock<IValidator<ConfigureGitHubPersonalAccessTokenCommand>> validatorMock = new Mock<IValidator<ConfigureGitHubPersonalAccessTokenCommand>>();
            validatorMock
                .Setup(validator => validator.ValidateAsync(It.IsAny<ConfigureGitHubPersonalAccessTokenCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ValidationResult());

            GitHubPersonalAccessTokenInspectionResult inspection = new GitHubPersonalAccessTokenInspectionResult(
                true,
                true,
                false,
                true,
                "octocat",
                new List<string> { "repo", "workflow", "read:org" },
                new List<string>(),
                new List<string>(),
                null);

            Mock<IGitHubPersonalAccessTokenInspector> inspectorMock = new Mock<IGitHubPersonalAccessTokenInspector>();
            inspectorMock
                .Setup(inspector => inspector.InspectAsync("github_pat_token", GitHubPersonalAccessTokenRequirements.RequiredScopes, It.IsAny<CancellationToken>()))
                .ReturnsAsync(inspection);

            Mock<ILogger<ConfigureGitHubPersonalAccessTokenCommandHandler>> loggerMock = new Mock<ILogger<ConfigureGitHubPersonalAccessTokenCommandHandler>>();

            ConfigureGitHubPersonalAccessTokenCommandHandler handler = new ConfigureGitHubPersonalAccessTokenCommandHandler(
                storeMock.Object,
                validatorMock.Object,
                loggerMock.Object,
                inspectorMock.Object);

            ConfigureGitHubPersonalAccessTokenCommand command = new ConfigureGitHubPersonalAccessTokenCommand("github_pat_token");

            ConfigureGitHubPersonalAccessTokenResultDto result = await handler.Handle(command, CancellationToken.None);

            result.Configured.Should().BeTrue();
            result.Validation.TokenAccepted.Should().BeTrue();
            result.Validation.HasRequiredPermissions.Should().BeTrue();
            result.Validation.Login.Should().Be("octocat");
            storeMock.Verify();
        }
    }
}
