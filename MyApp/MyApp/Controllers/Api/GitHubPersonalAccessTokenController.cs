using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MyApp.Application.GitHubPersonalAccessToken.Commands.ConfigureGitHubPersonalAccessToken;
using MyApp.Application.GitHubPersonalAccessToken.DTOs;
using MyApp.Application.GitHubPersonalAccessToken.Queries.GetGitHubPersonalAccessTokenStatus;
using MyApp.Models.Profile;
using FluentValidation.Results;

namespace MyApp.Controllers.Api
{
    [ApiController]
    [Route("api/github/personal-access-token")]
    [Produces("application/json")]
    public sealed class GitHubPersonalAccessTokenController : ControllerBase
    {
        private readonly IMediator mediator;
        private readonly ILogger<GitHubPersonalAccessTokenController> logger;

        public GitHubPersonalAccessTokenController(IMediator mediator, ILogger<GitHubPersonalAccessTokenController> logger)
        {
            this.mediator = mediator;
            this.logger = logger;
        }

        [HttpGet("status")]
        [ProducesResponseType(typeof(GitHubPersonalAccessTokenStatusDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetStatus(CancellationToken cancellationToken)
        {
            GitHubPersonalAccessTokenStatusDto status = await mediator.Send(new GetGitHubPersonalAccessTokenStatusQuery(), cancellationToken);
            return Ok(status);
        }

        [HttpPost]
        [ProducesResponseType(typeof(ConfigureGitHubPersonalAccessTokenResultDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Configure([FromBody] GitHubPersonalAccessTokenRequest request, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            try
            {
                ConfigureGitHubPersonalAccessTokenCommand command = new ConfigureGitHubPersonalAccessTokenCommand(request.Token);
                ConfigureGitHubPersonalAccessTokenResultDto result = await mediator.Send(command, cancellationToken);
                return Ok(result);
            }
            catch (ValidationException exception)
            {
                logger.LogWarning(exception, "Validation failure while configuring GitHub personal access token.");
                foreach (ValidationFailure failure in exception.Errors)
                {
                    ModelState.AddModelError(failure.PropertyName, failure.ErrorMessage);
                }

                return ValidationProblem(ModelState);
            }
            catch (System.Exception exception)
            {
                logger.LogError(exception, "Unexpected error while configuring GitHub personal access token.");
                ProblemDetails problem = new ProblemDetails
                {
                    Title = "Error al guardar el token",
                    Detail = "Ocurrió un error inesperado al guardar el token personal de GitHub.",
                    Status = StatusCodes.Status500InternalServerError
                };

                return StatusCode(StatusCodes.Status500InternalServerError, problem);
            }
        }
    }
}
