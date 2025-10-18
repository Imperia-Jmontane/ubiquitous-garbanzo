using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using MyApp.Application.GitHubOAuth.Commands.LinkGitHubAccount;
using MyApp.Application.GitHubOAuth.Commands.StartGitHubOAuth;
using MyApp.Application.GitHubOAuth.DTOs;
using MyApp.Application.GitHubOAuth.Exceptions;
using MyApp.Controllers.Api;
using MyApp.Models.GitHubAuth;
using Xunit;

namespace MyApp.Tests.Controllers
{
    public sealed class GitHubAuthControllerTests
    {
        [Fact]
        public async Task Start_ShouldReturnOkWithResult()
        {
            Mock<IMediator> mediatorMock = new Mock<IMediator>();
            Mock<ILogger<GitHubAuthController>> loggerMock = new Mock<ILogger<GitHubAuthController>>();

            Guid userId = Guid.NewGuid();
            StartGitHubOAuthResultDto dto = new StartGitHubOAuthResultDto(userId, "https://github.com/login/oauth/authorize?state=abc", "abc", new List<string> { "repo", "workflow", "read:user" }, DateTimeOffset.UtcNow.AddMinutes(5), true);
            mediatorMock.Setup(mediator => mediator.Send(It.IsAny<StartGitHubOAuthCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(dto);

            GitHubAuthController controller = new GitHubAuthController(mediatorMock.Object, loggerMock.Object);
            GitHubOAuthStartRequest request = new GitHubOAuthStartRequest
            {
                UserId = userId,
                RedirectUri = "https://localhost/signin-github"
            };

            IActionResult response = await controller.Start(request, CancellationToken.None);

            OkObjectResult okResult = Assert.IsType<OkObjectResult>(response);
            StartGitHubOAuthResultDto resultDto = Assert.IsType<StartGitHubOAuthResultDto>(okResult.Value);
            Assert.Equal(userId, resultDto.UserId);
        }

        [Fact]
        public async Task Start_ShouldReturnServiceUnavailableWhenSecretsMissing()
        {
            Mock<IMediator> mediatorMock = new Mock<IMediator>();
            Mock<ILogger<GitHubAuthController>> loggerMock = new Mock<ILogger<GitHubAuthController>>();

            mediatorMock.Setup(mediator => mediator.Send(It.IsAny<StartGitHubOAuthCommand>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Not configured"));

            GitHubAuthController controller = new GitHubAuthController(mediatorMock.Object, loggerMock.Object);
            GitHubOAuthStartRequest request = new GitHubOAuthStartRequest
            {
                UserId = Guid.NewGuid(),
                RedirectUri = "https://localhost/signin-github"
            };

            IActionResult response = await controller.Start(request, CancellationToken.None);

            ObjectResult problemResult = Assert.IsType<ObjectResult>(response);
            Assert.Equal(StatusCodes.Status503ServiceUnavailable, problemResult.StatusCode);
        }

        [Fact]
        public async Task Callback_ShouldReturnBadRequestWhenStateInvalid()
        {
            Mock<IMediator> mediatorMock = new Mock<IMediator>();
            Mock<ILogger<GitHubAuthController>> loggerMock = new Mock<ILogger<GitHubAuthController>>();

            mediatorMock.Setup(mediator => mediator.Send(It.IsAny<LinkGitHubAccountCommand>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidGitHubOAuthStateException("Invalid state."));

            GitHubAuthController controller = new GitHubAuthController(mediatorMock.Object, loggerMock.Object);
            GitHubOAuthCallbackRequest request = new GitHubOAuthCallbackRequest
            {
                UserId = Guid.NewGuid(),
                Code = "code",
                State = "state"
            };

            IActionResult response = await controller.Callback(request, CancellationToken.None);

            BadRequestObjectResult badRequest = Assert.IsType<BadRequestObjectResult>(response);
            ProblemDetails problem = Assert.IsType<ProblemDetails>(badRequest.Value);
            Assert.Equal("Invalid OAuth state", problem.Title);
        }
    }
}
