using FluentValidation;

namespace MyApp.Application.GitHubOAuth.Commands.ConfigureGitHubOAuthSecrets
{
    public sealed class ConfigureGitHubOAuthSecretsCommandValidator : AbstractValidator<ConfigureGitHubOAuthSecretsCommand>
    {
        public ConfigureGitHubOAuthSecretsCommandValidator()
        {
            RuleFor(command => command.ClientId)
                .NotEmpty()
                .MaximumLength(200);

            RuleFor(command => command.ClientSecret)
                .NotEmpty()
                .MinimumLength(20)
                .MaximumLength(400);

            RuleFor(command => command.SetupPassword)
                .NotEmpty()
                .MaximumLength(200);
        }
    }
}
