#nullable enable
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using MyApp.Application.Abstractions;
using MyApp.Application.GitHubOAuth.Configuration;
using MyApp.Infrastructure.GitHub;
using Xunit;

namespace MyApp.Tests.Infrastructure.GitHub
{
    public sealed class GitHubOAuthSettingsProviderTests
    {
        [Fact]
        public async Task GetSettingsAsync_ShouldReturnConfiguredWhenSecretsPresent()
        {
            GitHubOAuthOptions options = new GitHubOAuthOptions
            {
                AuthorizationEndpoint = "https://github.com/login/oauth/authorize",
                TokenEndpoint = "https://github.com/login/oauth/access_token",
                ClientId = "placeholder",
                ClientSecret = "placeholder",
                Scopes = { "repo" }
            };

            Mock<IOptionsMonitor<GitHubOAuthOptions>> optionsMonitorMock = new Mock<IOptionsMonitor<GitHubOAuthOptions>>();
            optionsMonitorMock.SetupGet(monitor => monitor.CurrentValue).Returns(options);

            Mock<ISecretProvider> secretProviderMock = new Mock<ISecretProvider>();
            secretProviderMock.Setup(provider => provider.GetSecretAsync("GitHubClientId", It.IsAny<CancellationToken>()))
                .ReturnsAsync("client-id");
            secretProviderMock.Setup(provider => provider.GetSecretAsync("GitHubClientSecret", It.IsAny<CancellationToken>()))
                .ReturnsAsync("client-secret");

            GitHubOAuthSettingsProvider provider = new GitHubOAuthSettingsProvider(optionsMonitorMock.Object, secretProviderMock.Object);

            GitHubOAuthSettings settings = await provider.GetSettingsAsync(CancellationToken.None);

            settings.IsConfigured.Should().BeTrue();
            settings.ClientId.Should().Be("client-id");
        }

        [Fact]
        public async Task GetSettingsAsync_ShouldReturnNotConfiguredWhenOnlyPlaceholderAvailable()
        {
            GitHubOAuthOptions options = new GitHubOAuthOptions
            {
                AuthorizationEndpoint = "https://github.com/login/oauth/authorize",
                TokenEndpoint = "https://github.com/login/oauth/access_token",
                ClientId = "${GITHUB__CLIENT_ID}",
                ClientSecret = "${GITHUB__CLIENT_SECRET}",
                Scopes = { "repo" }
            };

            Mock<IOptionsMonitor<GitHubOAuthOptions>> optionsMonitorMock = new Mock<IOptionsMonitor<GitHubOAuthOptions>>();
            optionsMonitorMock.SetupGet(monitor => monitor.CurrentValue).Returns(options);

            Mock<ISecretProvider> secretProviderMock = new Mock<ISecretProvider>();
            secretProviderMock.Setup(provider => provider.GetSecretAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string?)null);

            GitHubOAuthSettingsProvider provider = new GitHubOAuthSettingsProvider(optionsMonitorMock.Object, secretProviderMock.Object);

            GitHubOAuthSettings settings = await provider.GetSettingsAsync(CancellationToken.None);

            settings.IsConfigured.Should().BeFalse();
            settings.ClientId.Should().Be(string.Empty);
        }
    }
}
