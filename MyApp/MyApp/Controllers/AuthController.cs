using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MyApp.Application.Abstractions;
using MyApp.Application.GitHubOAuth.Commands.LinkGitHubAccount;
using MyApp.Application.GitHubOAuth.DTOs;
using MyApp.Application.GitHubOAuth.Exceptions;
using MyApp.Domain.Identity;

namespace MyApp.Controllers
{
    public sealed class AuthController : Controller
    {
        private readonly IMediator mediator;
        private readonly IGitHubOAuthStateRepository stateRepository;
        private readonly ILogger<AuthController> logger;

        public AuthController(IMediator mediator, IGitHubOAuthStateRepository stateRepository, ILogger<AuthController> logger)
        {
            this.mediator = mediator;
            this.stateRepository = stateRepository;
            this.logger = logger;
        }

        [HttpGet("/signin-github")]
        public async Task<IActionResult> GitHubCallback([FromQuery] string? code, [FromQuery] string? state, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(state))
            {
                TempData["GitHubLinkError"] = "No se recibió el código de autorización de GitHub.";
                return RedirectToAction("Index", "Profile");
            }

            GitHubOAuthState? oauthState = await stateRepository.GetAsync(state, cancellationToken);
            if (oauthState == null)
            {
                TempData["GitHubLinkError"] = "El estado de OAuth no es válido o expiró.";
                return RedirectToAction("Index", "Profile");
            }

            try
            {
                LinkGitHubAccountCommand command = new LinkGitHubAccountCommand(oauthState.UserId, code, state);
                LinkGitHubAccountResultDto result = await mediator.Send(command, cancellationToken);
                string successMessage = result.IsNewConnection
                    ? "La cuenta de GitHub se vinculó correctamente."
                    : "Los tokens de GitHub se actualizaron correctamente.";

                TempData["GitHubLinkSuccess"] = successMessage;
                return RedirectToAction("Index", "Profile");
            }
            catch (InvalidGitHubOAuthStateException exception)
            {
                logger.LogWarning(exception, "Invalid OAuth state during GitHub callback.");
                TempData["GitHubLinkError"] = "El estado de GitHub no es válido";
                return RedirectToAction("Index", "Profile");
            }
            catch (System.Exception exception)
            {
                logger.LogError(exception, "Failed to complete GitHub OAuth callback.");
                TempData["GitHubLinkError"] = "Ocurrió un error al completar la vinculación con GitHub.";
                return RedirectToAction("Index", "Profile");
            }
        }
    }
}
