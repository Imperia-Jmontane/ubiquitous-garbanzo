using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MyApp.Application.GitHubOAuth.Commands.LinkGitHubAccount;
using MyApp.Application.GitHubOAuth.Commands.StartGitHubOAuth;
using MyApp.Application.GitHubOAuth.DTOs;
using MyApp.Application.GitHubOAuth.Exceptions;
using MyApp.Models.GitHubAuth;
using Swashbuckle.AspNetCore.Annotations;

namespace MyApp.Controllers.Api
{
    [ApiController]
    [Route("api/auth/github")]
    [Produces("application/json")]
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
        [SwaggerOperation(Summary = "Start GitHub OAuth flow", Description = "Generates a state token and authorization URL requiring repo, workflow, and read:user scopes.", OperationId = "StartGitHubOAuth")]
        [ProducesResponseType(typeof(StartGitHubOAuthResultDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Start([FromBody] GitHubOAuthStartRequest request, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            try
            {
                StartGitHubOAuthCommand command = new StartGitHubOAuthCommand(request.UserId, request.RedirectUri);
                StartGitHubOAuthResultDto result = await mediator.Send(command, cancellationToken);

                return Ok(result);
            }
            catch (ValidationException exception)
            {
                logger.LogWarning(exception, "Validation failure when starting GitHub OAuth for user {UserId}", request.UserId);
                return CreateValidationProblem(exception);
            }
            catch (System.Exception exception)
            {
                logger.LogError(exception, "Unexpected error when starting GitHub OAuth for user {UserId}", request.UserId);
                ProblemDetails problem = new ProblemDetails
                {
                    Title = "Unexpected error",
                    Detail = "An unexpected error occurred while starting the GitHub OAuth flow.",
                    Status = StatusCodes.Status500InternalServerError
                };
                return StatusCode(StatusCodes.Status500InternalServerError, problem);
            }
        }

        [HttpPost("callback")]
        [SwaggerOperation(Summary = "Complete GitHub OAuth flow", Description = "Exchanges the authorization code for tokens, persists them, and emits an audit event.", OperationId = "CompleteGitHubOAuth")]
        [ProducesResponseType(typeof(LinkGitHubAccountResultDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Callback([FromBody] GitHubOAuthCallbackRequest request, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            try
            {
                LinkGitHubAccountCommand command = new LinkGitHubAccountCommand(request.UserId, request.Code, request.State);
                LinkGitHubAccountResultDto result = await mediator.Send(command, cancellationToken);

                return Ok(result);
            }
            catch (ValidationException exception)
            {
                logger.LogWarning(exception, "Validation failure when completing GitHub OAuth for user {UserId}", request.UserId);
                return CreateValidationProblem(exception);
            }
            catch (InvalidGitHubOAuthStateException exception)
            {
                logger.LogWarning(exception, "Invalid GitHub OAuth state for user {UserId}", request.UserId);
                ProblemDetails problem = new ProblemDetails
                {
                    Title = "Invalid OAuth state",
                    Detail = exception.Message,
                    Status = StatusCodes.Status400BadRequest
                };
                return BadRequest(problem);
            }
            catch (System.Exception exception)
            {
                logger.LogError(exception, "Unexpected error when completing GitHub OAuth for user {UserId}", request.UserId);
                ProblemDetails problem = new ProblemDetails
                {
                    Title = "Unexpected error",
                    Detail = "An unexpected error occurred while processing the GitHub OAuth callback.",
                    Status = StatusCodes.Status500InternalServerError
                };
                return StatusCode(StatusCodes.Status500InternalServerError, problem);
            }
        }

        private IActionResult CreateValidationProblem(ValidationException exception)
        {
            Dictionary<string, List<string>> aggregatedErrors = new Dictionary<string, List<string>>();
            foreach (FluentValidation.Results.ValidationFailure failure in exception.Errors)
            {
                if (!aggregatedErrors.TryGetValue(failure.PropertyName, out List<string>? messages))
                {
                    messages = new List<string>();
                    aggregatedErrors[failure.PropertyName] = messages;
                }

                messages.Add(failure.ErrorMessage);
            }

            Dictionary<string, string[]> formattedErrors = new Dictionary<string, string[]>();
            foreach (KeyValuePair<string, List<string>> entry in aggregatedErrors)
            {
                formattedErrors[entry.Key] = entry.Value.ToArray();
            }

            ValidationProblemDetails problemDetails = new ValidationProblemDetails(formattedErrors)
            {
                Title = "Validation failed",
                Status = StatusCodes.Status400BadRequest
            };

            return BadRequest(problemDetails);
        }
    }
}
