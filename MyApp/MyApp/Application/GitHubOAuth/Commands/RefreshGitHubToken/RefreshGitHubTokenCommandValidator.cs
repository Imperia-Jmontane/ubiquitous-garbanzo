using FluentValidation;

namespace MyApp.Application.GitHubOAuth.Commands.RefreshGitHubToken
{
    public sealed class RefreshGitHubTokenCommandValidator : AbstractValidator<RefreshGitHubTokenCommand>
    {
        public RefreshGitHubTokenCommandValidator()
        {
            RuleFor(command => command.State)
                .NotEmpty()
                .WithMessage("The state is required.")
                .MaximumLength(200)
                .WithMessage("The state value cannot exceed 200 characters.");

            RuleFor(command => command.RedirectUri)
                .NotEmpty()
                .WithMessage("The redirect URI is required.")
                .Must(uri => Uri.TryCreate(uri, UriKind.Absolute, out Uri? parsed) && (parsed.Scheme == "https" || parsed.Scheme == "http"))
                .WithMessage("The redirect URI must be absolute and use HTTP or HTTPS.");
        }
    }
}
