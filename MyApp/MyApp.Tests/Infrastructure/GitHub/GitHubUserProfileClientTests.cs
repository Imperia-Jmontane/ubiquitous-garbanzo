#nullable enable
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using MyApp.Application.Abstractions;
using MyApp.Infrastructure.GitHub;
using Xunit;

namespace MyApp.Tests.Infrastructure.GitHub
{
    public sealed class GitHubUserProfileClientTests
    {
        [Fact]
        public async Task GetProfileAsync_ShouldReturnProfileAndOrganizations()
        {
            TestHttpMessageHandler messageHandler = new TestHttpMessageHandler();
            messageHandler.ResponseFactory = request =>
            {
                if (request.RequestUri == null)
                {
                    throw new InvalidOperationException("Missing request URI.");
                }

                if (request.RequestUri.AbsolutePath.Equals("/user", StringComparison.Ordinal))
                {
                    string content = JsonSerializer.Serialize(new
                    {
                        login = "octocat",
                        name = "Octo Cat",
                        email = "octo@example.com",
                        avatar_url = "https://avatars.githubusercontent.com/u/1",
                        html_url = "https://github.com/octocat"
                    });
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(content)
                    };
                }

                if (request.RequestUri.AbsolutePath.Equals("/user/orgs", StringComparison.Ordinal))
                {
                    string content = JsonSerializer.Serialize(new[]
                    {
                        new { login = "github" },
                        new { login = "codex" }
                    });
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(content)
                    };
                }

                return new HttpResponseMessage(HttpStatusCode.NotFound)
                {
                    Content = new StringContent("{}")
                };
            };

            HttpClient httpClient = new HttpClient(messageHandler)
            {
                BaseAddress = new Uri("https://api.github.com/", UriKind.Absolute)
            };

            Mock<ILogger<GitHubUserProfileClient>> loggerMock = new Mock<ILogger<GitHubUserProfileClient>>();
            GitHubUserProfileClient client = new GitHubUserProfileClient(httpClient, loggerMock.Object);

            GitHubUserProfileInfo profile = await client.GetProfileAsync("token", CancellationToken.None);

            profile.Login.Should().Be("octocat");
            profile.Email.Should().Be("octo@example.com");
            profile.ProfileUrl.Should().Be("https://github.com/octocat");
            profile.Organizations.Should().Contain(new[] { "github", "codex" });
            messageHandler.Requests.Should().HaveCount(2);
            messageHandler.Requests[0].Headers.Authorization.Should().NotBeNull();
            messageHandler.Requests[0].Headers.Authorization!.Scheme.Should().Be("Bearer");
        }

        [Fact]
        public async Task GetProfileAsync_ShouldThrowWhenUserCallFails()
        {
            TestHttpMessageHandler messageHandler = new TestHttpMessageHandler();
            messageHandler.ResponseFactory = _ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent("{}")
            };

            HttpClient httpClient = new HttpClient(messageHandler)
            {
                BaseAddress = new Uri("https://api.github.com/", UriKind.Absolute)
            };

            Mock<ILogger<GitHubUserProfileClient>> loggerMock = new Mock<ILogger<GitHubUserProfileClient>>();
            GitHubUserProfileClient client = new GitHubUserProfileClient(httpClient, loggerMock.Object);

            await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetProfileAsync("token", CancellationToken.None));
        }

        [Fact]
        public async Task GetProfileAsync_ShouldReturnEmptyOrganizationsWhenForbidden()
        {
            TestHttpMessageHandler messageHandler = new TestHttpMessageHandler();
            messageHandler.ResponseFactory = request =>
            {
                if (request.RequestUri == null)
                {
                    throw new InvalidOperationException("Missing request URI.");
                }

                if (request.RequestUri.AbsolutePath.Equals("/user", StringComparison.Ordinal))
                {
                    string content = JsonSerializer.Serialize(new
                    {
                        login = "octocat",
                        name = "Octo Cat",
                        email = "octo@example.com",
                        avatar_url = "https://avatars.githubusercontent.com/u/1",
                        html_url = "https://github.com/octocat"
                    });
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(content)
                    };
                }

                return new HttpResponseMessage(HttpStatusCode.Forbidden)
                {
                    Content = new StringContent("{}")
                };
            };

            HttpClient httpClient = new HttpClient(messageHandler)
            {
                BaseAddress = new Uri("https://api.github.com/", UriKind.Absolute)
            };

            Mock<ILogger<GitHubUserProfileClient>> loggerMock = new Mock<ILogger<GitHubUserProfileClient>>();
            GitHubUserProfileClient client = new GitHubUserProfileClient(httpClient, loggerMock.Object);

            GitHubUserProfileInfo profile = await client.GetProfileAsync("token", CancellationToken.None);

            profile.Organizations.Should().BeEmpty();
        }

        private sealed class TestHttpMessageHandler : HttpMessageHandler
        {
            public List<HttpRequestMessage> Requests { get; } = new List<HttpRequestMessage>();

            public Func<HttpRequestMessage, HttpResponseMessage> ResponseFactory { get; set; } = _ => new HttpResponseMessage(HttpStatusCode.NotFound);

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Requests.Add(request);
                HttpResponseMessage response = ResponseFactory(request);
                return Task.FromResult(response);
            }
        }
    }
}
