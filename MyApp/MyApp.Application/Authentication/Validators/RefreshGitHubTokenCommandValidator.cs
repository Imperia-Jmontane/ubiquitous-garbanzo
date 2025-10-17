using System;
using FluentValidation;
using MyApp.Application.Authentication.Commands;

namespace MyApp.Application.Authentication.Validators
{
    public sealed class RefreshGitHubTokenCommandValidator : AbstractValidator<RefreshGitHubTokenCommand>
    {
        public RefreshGitHubTokenCommandValidator()
        {
            RuleFor(command => command.UserId).NotEqual(Guid.Empty);
        }
    }
}
