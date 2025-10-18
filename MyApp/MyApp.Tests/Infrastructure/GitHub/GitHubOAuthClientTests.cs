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
using MyApp.Application.GitHubOAuth.Configuration;
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
            IOptions<GitHubOAuthOptions> optionsWrapper = Options.Create(options);

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

        [Fact]
        public async Task RefreshTokenAsync_ShouldIncludeRefreshGrantType()
        {
            Mock<IGitCredentialStore> credentialStoreMock = new Mock<IGitCredentialStore>();
            credentialStoreMock.Setup(store => store.GetClientCredentialsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new GitHubOAuthClientCredentials("client", "secret"));

            Mock<ILogger<GitHubOAuthClient>> loggerMock = new Mock<ILogger<GitHubOAuthClient>>();
            GitHubOAuthOptions options = new GitHubOAuthOptions { TokenEndpoint = "https://example.com/token" };
            IOptions<GitHubOAuthOptions> optionsWrapper = Options.Create(options);

            TestHttpMessageHandler messageHandler = new TestHttpMessageHandler();
            HttpClient httpClient = new HttpClient(messageHandler);

            GitHubOAuthClient client = new GitHubOAuthClient(httpClient, credentialStoreMock.Object, optionsWrapper, loggerMock.Object);

            messageHandler.ResponseContent = JsonSerializer.Serialize(new
            {
                access_token = "token",
                refresh_token = "refresh",
                expires_in = 0,
                token_type = "bearer",
                scope = "repo",
                node_id = "node"
            });

            GitHubOAuthTokenResponse response = await client.RefreshTokenAsync(new GitHubTokenRefreshRequest("refresh"), CancellationToken.None);

            response.ExpiresIn.Should().Be(TimeSpan.FromSeconds(3600));
            messageHandler.LastRequest.Should().NotBeNull();
            HttpRequestMessage lastRequest = messageHandler.LastRequest ?? throw new InvalidOperationException("No request recorded.");
            messageHandler.LastContent.Should().Contain("\"grant_type\":\"refresh_token\"");
        }

        [Fact]
        public async Task SendRequestAsync_ShouldThrowWhenResponseIsNotSuccessful()
        {
            Mock<IGitCredentialStore> credentialStoreMock = new Mock<IGitCredentialStore>();
            credentialStoreMock.Setup(store => store.GetClientCredentialsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new GitHubOAuthClientCredentials("client", "secret"));

            Mock<ILogger<GitHubOAuthClient>> loggerMock = new Mock<ILogger<GitHubOAuthClient>>();
            GitHubOAuthOptions options = new GitHubOAuthOptions { TokenEndpoint = "https://example.com/token" };
            IOptions<GitHubOAuthOptions> optionsWrapper = Options.Create(options);

            FailingHttpMessageHandler messageHandler = new FailingHttpMessageHandler();
            HttpClient httpClient = new HttpClient(messageHandler);

            GitHubOAuthClient client = new GitHubOAuthClient(httpClient, credentialStoreMock.Object, optionsWrapper, loggerMock.Object);

            GitHubCodeExchangeRequest request = new GitHubCodeExchangeRequest("code", "https://app/callback", "state");

            await Assert.ThrowsAsync<InvalidOperationException>(() => client.ExchangeCodeAsync(request, CancellationToken.None));
        }

        [Fact]
        public async Task SendRequestAsync_ShouldThrowWhenResponseBodyIsEmpty()
        {
            Mock<IGitCredentialStore> credentialStoreMock = new Mock<IGitCredentialStore>();
            credentialStoreMock.Setup(store => store.GetClientCredentialsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new GitHubOAuthClientCredentials("client", "secret"));

            Mock<ILogger<GitHubOAuthClient>> loggerMock = new Mock<ILogger<GitHubOAuthClient>>();
            GitHubOAuthOptions options = new GitHubOAuthOptions { TokenEndpoint = "https://example.com/token" };
            IOptions<GitHubOAuthOptions> optionsWrapper = Options.Create(options);

            EmptyResponseHttpMessageHandler messageHandler = new EmptyResponseHttpMessageHandler();
            HttpClient httpClient = new HttpClient(messageHandler);

            GitHubOAuthClient client = new GitHubOAuthClient(httpClient, credentialStoreMock.Object, optionsWrapper, loggerMock.Object);

            GitHubTokenRefreshRequest request = new GitHubTokenRefreshRequest("refresh");

            await Assert.ThrowsAsync<InvalidOperationException>(() => client.RefreshTokenAsync(request, CancellationToken.None));
        }

        private sealed class TestHttpMessageHandler : HttpMessageHandler
        {
            public HttpRequestMessage? LastRequest { get; private set; }

            public string ResponseContent { get; set; } = string.Empty;

            public string LastContent { get; private set; } = string.Empty;

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                LastRequest = request;
                if (request.Content != null)
                {
                    LastContent = await request.Content.ReadAsStringAsync(cancellationToken);
                }
                HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(ResponseContent)
                };

                return response;
            }
        }

        private sealed class FailingHttpMessageHandler : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    Content = new StringContent("{}")
                };

                return Task.FromResult(response);
            }
        }

        private sealed class EmptyResponseHttpMessageHandler : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("null")
                };

                return Task.FromResult(response);
            }
        }
    }
}
