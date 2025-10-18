using System;
using FluentValidation;

namespace MyApp.Application.GitHubOAuth.Commands.StartGitHubOAuth
{
    public sealed class StartGitHubOAuthCommandValidator : AbstractValidator<StartGitHubOAuthCommand>
    {
        public StartGitHubOAuthCommandValidator()
        {
            RuleFor(command => command.RedirectUri)
                .NotEmpty()
                .WithMessage("The redirect URI is required.")
                .Must(uri => Uri.TryCreate(uri, UriKind.Absolute, out Uri? parsed) && (parsed.Scheme == "https" || parsed.Scheme == "http"))
                .WithMessage("The redirect URI must be absolute and use HTTP or HTTPS.");
        }
    }
}
