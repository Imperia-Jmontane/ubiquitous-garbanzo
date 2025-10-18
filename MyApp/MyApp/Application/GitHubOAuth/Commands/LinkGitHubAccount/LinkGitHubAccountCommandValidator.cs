using FluentValidation;

namespace MyApp.Application.GitHubOAuth.Commands.LinkGitHubAccount
{
    public sealed class LinkGitHubAccountCommandValidator : AbstractValidator<LinkGitHubAccountCommand>
    {
        public LinkGitHubAccountCommandValidator()
        {
            RuleFor(command => command.Code)
                .NotEmpty()
                .WithMessage("The authorization code is required.")
                .MaximumLength(200)
                .WithMessage("The authorization code cannot exceed 200 characters.");

            RuleFor(command => command.State)
                .NotEmpty()
                .WithMessage("The state is required.")
                .MaximumLength(200)
                .WithMessage("The state value cannot exceed 200 characters.");
        }
    }
}
