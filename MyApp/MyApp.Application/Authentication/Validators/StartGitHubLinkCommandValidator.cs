using System;
using FluentValidation;
using MyApp.Application.Authentication.Commands;

namespace MyApp.Application.Authentication.Validators
{
    public sealed class StartGitHubLinkCommandValidator : AbstractValidator<StartGitHubLinkCommand>
    {
        public StartGitHubLinkCommandValidator()
        {
            RuleFor(command => command.UserId).NotEqual(Guid.Empty);
            RuleFor(command => command.RedirectUri).NotEmpty().Must(uri => Uri.TryCreate(uri, UriKind.Absolute, out _)).WithMessage("The redirectUri must be an absolute URI.");
        }
    }
}
