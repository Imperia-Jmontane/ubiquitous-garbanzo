using FluentValidation;

namespace MyApp.Application.GitHubPersonalAccessToken.Commands.ConfigureGitHubPersonalAccessToken
{
    public sealed class ConfigureGitHubPersonalAccessTokenCommandValidator : AbstractValidator<ConfigureGitHubPersonalAccessTokenCommand>
    {
        public ConfigureGitHubPersonalAccessTokenCommandValidator()
        {
            RuleFor(command => command.Token)
                .NotEmpty().WithMessage("El token personal es obligatorio.")
                .MinimumLength(40).WithMessage("El token debe tener al menos 40 caracteres.")
                .Matches("^[A-Za-z0-9_]+$").WithMessage("El token solo puede contener caracteres alfanuméricos y guiones bajos.")
                .Must(value => value == null || value.Trim().Length == value.Length).WithMessage("El token no puede contener espacios al inicio o al final.");
        }
    }
}
