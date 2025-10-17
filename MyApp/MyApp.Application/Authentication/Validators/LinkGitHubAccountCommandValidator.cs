using System;
using FluentValidation;
using MyApp.Application.Authentication.Commands;

namespace MyApp.Application.Authentication.Validators
{
    public sealed class LinkGitHubAccountCommandValidator : AbstractValidator<LinkGitHubAccountCommand>
    {
        public LinkGitHubAccountCommandValidator()
        {
            RuleFor(command => command.Code).NotEmpty();
            RuleFor(command => command.State).NotEmpty();
            RuleFor(command => command.RedirectUri).NotEmpty().Must(uri => Uri.TryCreate(uri, UriKind.Absolute, out _)).WithMessage("The redirectUri must be an absolute URI.");
        }
    }
}
