using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using MyApp.Application.Authentication.Models;
using MyApp.Application.Common.Interfaces;
using MyApp.Infrastructure.Authentication;

namespace MyApp.Tests
{
    public sealed class GitHubOAuthClientTests
    {
        [Fact]
        public async Task ExchangeCodeForTokenAsync_Should_Return_Session()
        {
            DateTimeOffset now = new DateTimeOffset(2025, 10, 17, 11, 0, 0, TimeSpan.Zero);
            StubHttpMessageHandler handler = new StubHttpMessageHandler();
            HttpClient httpClient = new HttpClient(handler);

            GitHubOAuthOptions options = new GitHubOAuthOptions
            {
                ClientId = "client-id",
                ClientSecret = "client-secret",
                AuthorizationEndpoint = "https://github.com/login/oauth/authorize",
                TokenEndpoint = "https://github.com/login/oauth/access_token",
                UserEndpoint = "https://api.github.com/user",
                AllowedRedirectUris = { "https://app.example.com/callback" }
            };

            Mock<IDateTimeProvider> dateTimeProvider = new Mock<IDateTimeProvider>();
            dateTimeProvider.SetupGet(provider => provider.UtcNow).Returns(now);
            ILogger<GitHubOAuthClient> logger = Mock.Of<ILogger<GitHubOAuthClient>>();

            GitHubOAuthClient client = new GitHubOAuthClient(httpClient, Options.Create(options), dateTimeProvider.Object, logger);

            GitHubOAuthSession session = await client.ExchangeCodeForTokenAsync("code", "https://app.example.com/callback", CancellationToken.None);

            session.Identity.Login.Should().Be("octocat");
            session.Token.AccessToken.Should().Be("token-value");
            session.Token.AllowsRepositoryClone().Should().BeTrue();
        }

        [Fact]
        public void CreateAuthorizationInfo_Should_ReturnExpectedUrl()
        {
            GitHubOAuthOptions options = new GitHubOAuthOptions
            {
                ClientId = "client-id",
                AuthorizationEndpoint = "https://github.com/login/oauth/authorize",
                AllowedRedirectUris = { "https://app.example.com/callback" }
            };

            Mock<IDateTimeProvider> dateTimeProvider = new Mock<IDateTimeProvider>();
            ILogger<GitHubOAuthClient> logger = Mock.Of<ILogger<GitHubOAuthClient>>();
            HttpClient httpClient = new HttpClient(new StubHttpMessageHandler());
            GitHubOAuthClient client = new GitHubOAuthClient(httpClient, Options.Create(options), dateTimeProvider.Object, logger);

            string state = "state-value";
            GitHubAuthorizationInfo authorization = client.CreateAuthorizationInfo(state, "https://app.example.com/callback");

            authorization.State.Should().Be(state);
            authorization.AuthorizationUrl.Should().Contain("client_id=client-id");
            authorization.AuthorizationUrl.Should().Contain(Uri.EscapeDataString("https://app.example.com/callback"));
        }

        private sealed class StubHttpMessageHandler : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                if (request.RequestUri != null && request.RequestUri.AbsoluteUri.Contains("access_token", StringComparison.OrdinalIgnoreCase))
                {
                    string tokenPayload = "{\"access_token\":\"token-value\",\"refresh_token\":\"refresh-token\",\"expires_in\":3600,\"scope\":\"repo,read:user\"}";
                    HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(tokenPayload, Encoding.UTF8, "application/json")
                    };

                    return Task.FromResult(response);
                }

                if (request.RequestUri != null && request.RequestUri.AbsoluteUri.Contains("/user", StringComparison.OrdinalIgnoreCase))
                {
                    string userPayload = "{\"id\":123,\"login\":\"octocat\",\"name\":\"The Octocat\",\"avatar_url\":\"https://avatars/github.png\"}";
                    HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(userPayload, Encoding.UTF8, "application/json")
                    };

                    return Task.FromResult(response);
                }

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            }
        }
    }
}
