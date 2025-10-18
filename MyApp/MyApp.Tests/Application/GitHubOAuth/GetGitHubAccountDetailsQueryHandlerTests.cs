#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using MyApp.Application.Abstractions;
using MyApp.Application.GitHubOAuth.DTOs;
using MyApp.Application.GitHubOAuth.Queries.GetGitHubAccountDetails;
using MyApp.Domain.Identity;
using Xunit;

namespace MyApp.Tests.Application.GitHubOAuth
{
    public sealed class GetGitHubAccountDetailsQueryHandlerTests
    {
        [Fact]
        public async Task Handle_ShouldReturnNotLinked_WhenLoginDoesNotExist()
        {
            Guid userId = Guid.NewGuid();
            Mock<IUserExternalLoginRepository> repositoryMock = new Mock<IUserExternalLoginRepository>();
            repositoryMock.Setup(repository => repository.GetAsync(userId, "GitHub", It.IsAny<CancellationToken>()))
                .ReturnsAsync((UserExternalLogin?)null);

            Mock<IGitHubUserProfileClient> profileClientMock = new Mock<IGitHubUserProfileClient>();
            Mock<ILogger<GetGitHubAccountDetailsQueryHandler>> loggerMock = new Mock<ILogger<GetGitHubAccountDetailsQueryHandler>>();

            GetGitHubAccountDetailsQueryHandler handler = new GetGitHubAccountDetailsQueryHandler(
                repositoryMock.Object,
                profileClientMock.Object,
                loggerMock.Object);

            GetGitHubAccountDetailsQuery query = new GetGitHubAccountDetailsQuery(userId);

            GitHubAccountDetailsDto result = await handler.Handle(query, CancellationToken.None);

            result.IsLinked.Should().BeFalse();
            profileClientMock.Verify(client => client.GetProfileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Handle_ShouldReturnProfile_WhenLoginExists()
        {
            Guid userId = Guid.NewGuid();
            DateTimeOffset expiresAt = DateTimeOffset.UtcNow.AddHours(1);
            UserExternalLogin login = new UserExternalLogin(userId, "GitHub", "node", "token", "refresh", expiresAt);

            Mock<IUserExternalLoginRepository> repositoryMock = new Mock<IUserExternalLoginRepository>();
            repositoryMock.Setup(repository => repository.GetAsync(userId, "GitHub", It.IsAny<CancellationToken>()))
                .ReturnsAsync(login);

            List<string> organizations = new List<string> { "org-one", "org-two" };
            GitHubUserProfileInfo profileInfo = new GitHubUserProfileInfo("octocat", "Octo Cat", "octo@example.com", "https://avatars.githubusercontent.com/u/1", "https://github.com/octocat", organizations);

            Mock<IGitHubUserProfileClient> profileClientMock = new Mock<IGitHubUserProfileClient>();
            profileClientMock.Setup(client => client.GetProfileAsync(login.AccessToken, It.IsAny<CancellationToken>()))
                .ReturnsAsync(profileInfo);

            Mock<ILogger<GetGitHubAccountDetailsQueryHandler>> loggerMock = new Mock<ILogger<GetGitHubAccountDetailsQueryHandler>>();

            GetGitHubAccountDetailsQueryHandler handler = new GetGitHubAccountDetailsQueryHandler(
                repositoryMock.Object,
                profileClientMock.Object,
                loggerMock.Object);

            GetGitHubAccountDetailsQuery query = new GetGitHubAccountDetailsQuery(userId);

            GitHubAccountDetailsDto result = await handler.Handle(query, CancellationToken.None);

            result.IsLinked.Should().BeTrue();
            result.Profile.Should().NotBeNull();
            result.Profile!.Login.Should().Be("octocat");
            result.Profile.Organizations.Should().HaveCount(2);
            result.ProfileFetchError.Should().BeEmpty();
        }

        [Fact]
        public async Task Handle_ShouldReturnErrorMessage_WhenProfileFetchFails()
        {
            Guid userId = Guid.NewGuid();
            DateTimeOffset expiresAt = DateTimeOffset.UtcNow.AddHours(1);
            UserExternalLogin login = new UserExternalLogin(userId, "GitHub", "node", "token", "refresh", expiresAt);

            Mock<IUserExternalLoginRepository> repositoryMock = new Mock<IUserExternalLoginRepository>();
            repositoryMock.Setup(repository => repository.GetAsync(userId, "GitHub", It.IsAny<CancellationToken>()))
                .ReturnsAsync(login);

            Mock<IGitHubUserProfileClient> profileClientMock = new Mock<IGitHubUserProfileClient>();
            profileClientMock.Setup(client => client.GetProfileAsync(login.AccessToken, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("boom"));

            Mock<ILogger<GetGitHubAccountDetailsQueryHandler>> loggerMock = new Mock<ILogger<GetGitHubAccountDetailsQueryHandler>>();

            GetGitHubAccountDetailsQueryHandler handler = new GetGitHubAccountDetailsQueryHandler(
                repositoryMock.Object,
                profileClientMock.Object,
                loggerMock.Object);

            GetGitHubAccountDetailsQuery query = new GetGitHubAccountDetailsQuery(userId);

            GitHubAccountDetailsDto result = await handler.Handle(query, CancellationToken.None);

            result.IsLinked.Should().BeTrue();
            result.Profile.Should().BeNull();
            result.ProfileFetchError.Should().NotBeEmpty();
        }
    }
}
