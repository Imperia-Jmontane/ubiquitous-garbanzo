using System;
using System.Linq;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MyApp.Application.Authentication.Commands;
using MyApp.Application.Authentication.Models;
using MyApp.Domain.ValueObjects;
using Swashbuckle.AspNetCore.Annotations;
using MyApp.Models.Auth;

namespace MyApp.Controllers
{
    [ApiController]
    [Route("api/auth/github")]
    public sealed class GitHubAuthController : ControllerBase
    {
        private readonly IMediator mediator;
        private readonly ILogger<GitHubAuthController> logger;

        public GitHubAuthController(IMediator mediator, ILogger<GitHubAuthController> logger)
        {
            this.mediator = mediator;
            this.logger = logger;
        }

        [HttpPost("start")]
        [SwaggerOperation(Summary = "Starts the GitHub OAuth flow", Description = "Generates the authorization URL with the required repo and read:user scopes.")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(StartGitHubLinkResponse))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<StartGitHubLinkResponse>> StartLinkAsync([FromBody] StartGitHubLinkRequest request)
        {
            StartGitHubLinkCommand command = new StartGitHubLinkCommand(request.UserId, request.RedirectUri);
            try
            {
                GitHubAuthorizationInfo authorization = await mediator.Send(command);

                StartGitHubLinkResponse response = new StartGitHubLinkResponse
                {
                    AuthorizationUrl = authorization.AuthorizationUrl,
                    State = authorization.State,
                    Scopes = authorization.Scopes.ToList()
                };

                logger.LogInformation("GitHub authorization initiated for user {UserId} with state {State}.", request.UserId, response.State);

                return Ok(response);
            }
            catch (ValidationException exception)
            {
                return BadRequest(exception.Errors);
            }
            catch (InvalidOperationException exception)
            {
                return BadRequest(new { error = exception.Message });
            }
        }

        [HttpPost("callback")]
        [SwaggerOperation(Summary = "Completes the GitHub OAuth callback", Description = "Exchanges the authorization code for tokens and records the linked account.")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(CompleteGitHubLinkResponse))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<CompleteGitHubLinkResponse>> CompleteLinkAsync([FromBody] CompleteGitHubLinkRequest request)
        {
            try
            {
                LinkGitHubAccountCommand command = new LinkGitHubAccountCommand(request.UserId, request.Code, request.State, request.RedirectUri);
                LinkGitHubAccountResult result = await mediator.Send(command);

                CompleteGitHubLinkResponse response = new CompleteGitHubLinkResponse
                {
                    UserId = result.AccountLink.UserId,
                    GitHubLogin = result.AccountLink.Identity.Login,
                    DisplayName = result.AccountLink.Identity.DisplayName,
                    AvatarUrl = result.AccountLink.Identity.AvatarUrl,
                    CanClone = result.CanClone
                };

                logger.LogInformation("GitHub account {Login} linked for user {UserId}.", response.GitHubLogin, request.UserId);

                return Ok(response);
            }
            catch (ValidationException exception)
            {
                return BadRequest(exception.Errors);
            }
            catch (InvalidOperationException exception)
            {
                return BadRequest(new { error = exception.Message });
            }
        }

        [HttpPost("refresh")]
        [SwaggerOperation(Summary = "Refreshes the persisted GitHub token", Description = "Requests a new OAuth token using the stored refresh token to maintain clone permissions.")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(RefreshGitHubTokenResponse))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<RefreshGitHubTokenResponse>> RefreshTokenAsync([FromBody] RefreshGitHubTokenRequest request)
        {
            try
            {
                RefreshGitHubTokenCommand command = new RefreshGitHubTokenCommand(request.UserId);
                GitHubToken token = await mediator.Send(command);

                RefreshGitHubTokenResponse response = new RefreshGitHubTokenResponse
                {
                    CanClone = token.AllowsRepositoryClone()
                };

                logger.LogInformation("GitHub token refreshed for user {UserId}.", request.UserId);

                return Ok(response);
            }
            catch (ValidationException exception)
            {
                return BadRequest(exception.Errors);
            }
            catch (InvalidOperationException exception)
            {
                return BadRequest(new { error = exception.Message });
            }
        }
    }
}
