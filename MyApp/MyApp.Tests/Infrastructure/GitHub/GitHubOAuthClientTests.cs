#nullable enable
using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using MyApp.Application.Abstractions;
using MyApp.Application.GitHubOAuth.Models;
using MyApp.Infrastructure.GitHub;
using Xunit;

namespace MyApp.Tests.Infrastructure.GitHub
{
    public sealed class GitHubOAuthClientTests
    {
        [Fact]
        public async Task ExchangeCodeAsync_ShouldSendBasicAuthRequest()
        {
            Mock<IGitCredentialStore> credentialStoreMock = new Mock<IGitCredentialStore>();
            credentialStoreMock.Setup(store => store.GetClientCredentialsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new GitHubOAuthClientCredentials("client", "secret"));

            Mock<ILogger<GitHubOAuthClient>> loggerMock = new Mock<ILogger<GitHubOAuthClient>>();
            GitHubOAuthOptions options = new GitHubOAuthOptions { TokenEndpoint = "https://example.com/token" };
            IOptions<GitHubOAuthOptions> optionsWrapper = Microsoft.Extensions.Options.Options.Create(options);

            TestHttpMessageHandler messageHandler = new TestHttpMessageHandler();
            HttpClient httpClient = new HttpClient(messageHandler);

            GitHubOAuthClient client = new GitHubOAuthClient(httpClient, credentialStoreMock.Object, optionsWrapper, loggerMock.Object);

            GitHubCodeExchangeRequest request = new GitHubCodeExchangeRequest("code", "https://app/callback", "state");
            messageHandler.ResponseContent = JsonSerializer.Serialize(new
            {
                access_token = "token",
                refresh_token = "refresh",
                expires_in = 3600,
                token_type = "bearer",
                scope = "repo",
                node_id = "node"
            });

            GitHubOAuthTokenResponse response = await client.ExchangeCodeAsync(request, CancellationToken.None);

            response.AccessToken.Should().Be("token");
            messageHandler.LastRequest.Should().NotBeNull();
            HttpRequestMessage lastRequest = messageHandler.LastRequest ?? throw new InvalidOperationException("No request recorded.");
            lastRequest.Headers.Authorization.Should().NotBeNull();
            lastRequest.Headers.Authorization!.Scheme.Should().Be("Basic");
        }

        private sealed class TestHttpMessageHandler : HttpMessageHandler
        {
            public HttpRequestMessage? LastRequest { get; private set; }

            public string ResponseContent { get; set; } = string.Empty;

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                LastRequest = request;
                HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(ResponseContent)
                };

                return Task.FromResult(response);
            }
        }
    }
}
