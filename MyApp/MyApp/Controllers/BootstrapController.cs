using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MyApp.Application.GitHubOAuth.Commands.ConfigureGitHubOAuthSecrets;
using MyApp.Application.GitHubOAuth.DTOs;
using MyApp.Application.GitHubOAuth.Queries.GetGitHubOAuthStatus;
using MyApp.Models.Bootstrap;

namespace MyApp.Controllers
{
    public sealed class BootstrapController : Controller
    {
        private readonly IMediator mediator;
        private readonly ILogger<BootstrapController> logger;

        public BootstrapController(IMediator mediator, ILogger<BootstrapController> logger)
        {
            this.mediator = mediator;
            this.logger = logger;
        }

        [HttpGet("/bootstrap/github")]
        public async Task<IActionResult> GitHub(CancellationToken cancellationToken)
        {
            GitHubOAuthStatusDto status = await mediator.Send(new GetGitHubOAuthStatusQuery(), cancellationToken);
            GitHubBootstrapViewModel viewModel = CreateViewModel(status, new GitHubBootstrapFormModel(), null, success: false);
            return View("GitHubOAuth", viewModel);
        }

        [HttpPost("/bootstrap/github")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GitHub(GitHubBootstrapFormModel form, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                GitHubOAuthStatusDto status = await mediator.Send(new GetGitHubOAuthStatusQuery(), cancellationToken);
                GitHubBootstrapViewModel invalidViewModel = CreateViewModel(status, form, null, success: false);
                return View("GitHubOAuth", invalidViewModel);
            }

            try
            {
                ConfigureGitHubOAuthSecretsCommand command = new ConfigureGitHubOAuthSecretsCommand(form.ClientId, form.ClientSecret, form.SetupPassword);
                ConfigureGitHubOAuthSecretsResultDto result = await mediator.Send(command, cancellationToken);

                GitHubOAuthStatusDto status = await mediator.Send(new GetGitHubOAuthStatusQuery(), cancellationToken);
                GitHubBootstrapViewModel successViewModel = CreateViewModel(status, new GitHubBootstrapFormModel(), null, result.Configured);
                successViewModel.Success = result.Configured;
                return View("GitHubOAuth", successViewModel);
            }
            catch (InvalidOperationException exception)
            {
                logger.LogWarning(exception, "Failed to configure GitHub OAuth secrets via bootstrap page.");
                string errorMessage = "No se pudo validar el password temporal.";
                ModelState.AddModelError(string.Empty, errorMessage);
                GitHubOAuthStatusDto status = await mediator.Send(new GetGitHubOAuthStatusQuery(), cancellationToken);
                GitHubBootstrapViewModel errorViewModel = CreateViewModel(status, form, errorMessage, success: false);
                return View("GitHubOAuth", errorViewModel);
            }
        }

        private static GitHubBootstrapViewModel CreateViewModel(GitHubOAuthStatusDto status, GitHubBootstrapFormModel form, string? errorMessage, bool success)
        {
            List<string> scopes = new List<string>();
            foreach (string scope in status.Scopes)
            {
                scopes.Add(scope);
            }

            GitHubBootstrapViewModel viewModel = new GitHubBootstrapViewModel
            {
                IsConfigured = status.IsConfigured,
                ClientIdPreview = CreateClientIdPreview(status.ClientId),
                Scopes = scopes,
                Form = form,
                ErrorMessage = errorMessage,
                Success = success
            };

            return viewModel;
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
