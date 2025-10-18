using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MyApp.Application.GitHubOAuth.Commands.ConfigureGitHubOAuthSecrets;
using MyApp.Application.GitHubOAuth.DTOs;
using MyApp.Application.GitHubOAuth.Queries.GetGitHubOAuthStatus;
using MyApp.Models.Bootstrap;

namespace MyApp.Controllers.Api
{
    [ApiController]
    [Route("api/bootstrap/github")]
    [Produces("application/json")]
    public sealed class GitHubBootstrapController : ControllerBase
    {
        private readonly IMediator mediator;
        private readonly ILogger<GitHubBootstrapController> logger;

        public GitHubBootstrapController(IMediator mediator, ILogger<GitHubBootstrapController> logger)
        {
            this.mediator = mediator;
            this.logger = logger;
        }

        [HttpGet("status")]
        [ProducesResponseType(typeof(GitHubBootstrapStatusResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetStatus(CancellationToken cancellationToken)
        {
            GitHubOAuthStatusDto status = await mediator.Send(new GetGitHubOAuthStatusQuery(), cancellationToken);

            GitHubBootstrapStatusResponse response = new GitHubBootstrapStatusResponse
            {
                IsConfigured = status.IsConfigured,
                ClientIdPreview = CreateClientIdPreview(status.ClientId),
                Scopes = new List<string>(status.Scopes)
            };

            return Ok(response);
        }

        [HttpPost]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> Configure([FromBody] GitHubBootstrapRequest request, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            try
            {
                ConfigureGitHubOAuthSecretsCommand command = new ConfigureGitHubOAuthSecretsCommand(request.ClientId, request.ClientSecret, request.SetupPassword);
                ConfigureGitHubOAuthSecretsResultDto result = await mediator.Send(command, cancellationToken);
                if (!result.Configured)
                {
                    ProblemDetails problem = new ProblemDetails
                    {
                        Title = "Configuración incompleta",
                        Detail = "No se pudo completar la configuración de GitHub OAuth.",
                        Status = StatusCodes.Status500InternalServerError
                    };
                    return StatusCode(StatusCodes.Status500InternalServerError, problem);
                }

                return NoContent();
            }
            catch (System.InvalidOperationException exception)
            {
                logger.LogWarning(exception, "Attempt to configure GitHub OAuth secrets failed due to invalid setup password.");
                ProblemDetails problem = new ProblemDetails
                {
                    Title = "Password temporal inválido",
                    Detail = "El password temporal ingresado no es válido.",
                    Status = StatusCodes.Status403Forbidden
                };
                return StatusCode(StatusCodes.Status403Forbidden, problem);
            }
        }

        private static string CreateClientIdPreview(string clientId)
        {
            if (string.IsNullOrWhiteSpace(clientId))
            {
                return string.Empty;
            }

            if (clientId.Length <= 4)
            {
                return clientId;
            }

            string suffix = clientId.Substring(clientId.Length - 4);
            return string.Concat("••••", suffix);
        }
    }
}
