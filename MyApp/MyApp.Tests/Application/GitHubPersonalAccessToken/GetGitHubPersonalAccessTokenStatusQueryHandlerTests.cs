#nullable enable
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using MyApp.Application.Abstractions;
using MyApp.Application.GitHubPersonalAccessToken;
using MyApp.Application.GitHubPersonalAccessToken.DTOs;
using MyApp.Application.GitHubPersonalAccessToken.Models;
using MyApp.Application.GitHubPersonalAccessToken.Queries.GetGitHubPersonalAccessTokenStatus;
using Xunit;

namespace MyApp.Tests.Application.GitHubPersonalAccessToken
{
    public sealed class GetGitHubPersonalAccessTokenStatusQueryHandlerTests
    {
        [Fact]
        public async Task Handle_ShouldReturnTokenNotStored_WhenSecretIsMissing()
        {
            Mock<ISecretProvider> secretProviderMock = new Mock<ISecretProvider>();
            secretProviderMock
                .Setup(provider => provider.GetSecretAsync("GitHubPersonalAccessToken", It.IsAny<CancellationToken>()))
                .ReturnsAsync((string?)null);

            Mock<IGitHubPersonalAccessTokenInspector> inspectorMock = new Mock<IGitHubPersonalAccessTokenInspector>();

            GetGitHubPersonalAccessTokenStatusQueryHandler handler = new GetGitHubPersonalAccessTokenStatusQueryHandler(
                secretProviderMock.Object,
                inspectorMock.Object);

            GitHubPersonalAccessTokenStatusDto result = await handler.Handle(new GetGitHubPersonalAccessTokenStatusQuery(), CancellationToken.None);

            result.TokenStored.Should().BeFalse();
            result.IsConfigured.Should().BeFalse();
            result.Validation.Should().BeNull();
            inspectorMock.Verify(inspector => inspector.InspectAsync(It.IsAny<string>(), It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Handle_ShouldReturnValidationDetails_WhenTokenStored()
        {
            Mock<ISecretProvider> secretProviderMock = new Mock<ISecretProvider>();
            secretProviderMock
                .Setup(provider => provider.GetSecretAsync("GitHubPersonalAccessToken", It.IsAny<CancellationToken>()))
                .ReturnsAsync("github_pat_token");

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

            GetGitHubPersonalAccessTokenStatusQueryHandler handler = new GetGitHubPersonalAccessTokenStatusQueryHandler(
                secretProviderMock.Object,
                inspectorMock.Object);

            GitHubPersonalAccessTokenStatusDto result = await handler.Handle(new GetGitHubPersonalAccessTokenStatusQuery(), CancellationToken.None);

            result.TokenStored.Should().BeTrue();
            result.IsConfigured.Should().BeTrue();
            result.Validation.Should().NotBeNull();
            result.Validation!.Login.Should().Be("octocat");
            result.Validation.HasRequiredPermissions.Should().BeTrue();
        }
    }
}
