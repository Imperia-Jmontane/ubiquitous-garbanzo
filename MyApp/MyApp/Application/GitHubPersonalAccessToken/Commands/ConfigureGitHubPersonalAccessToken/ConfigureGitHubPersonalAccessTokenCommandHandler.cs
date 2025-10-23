using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Microsoft.Extensions.Logging;
using MyApp.Application.Abstractions;
using MyApp.Application.GitHubPersonalAccessToken;
using MyApp.Application.GitHubPersonalAccessToken.DTOs;
using MyApp.Application.GitHubPersonalAccessToken.Models;

namespace MyApp.Application.GitHubPersonalAccessToken.Commands.ConfigureGitHubPersonalAccessToken
{
    public sealed class ConfigureGitHubPersonalAccessTokenCommandHandler : IRequestHandler<ConfigureGitHubPersonalAccessTokenCommand, ConfigureGitHubPersonalAccessTokenResultDto>
    {
        private readonly IWritableSecretStore writableSecretStore;
        private readonly IValidator<ConfigureGitHubPersonalAccessTokenCommand> validator;
        private readonly ILogger<ConfigureGitHubPersonalAccessTokenCommandHandler> logger;
        private readonly IGitHubPersonalAccessTokenInspector personalAccessTokenInspector;

        public ConfigureGitHubPersonalAccessTokenCommandHandler(
            IWritableSecretStore writableSecretStore,
            IValidator<ConfigureGitHubPersonalAccessTokenCommand> validator,
            ILogger<ConfigureGitHubPersonalAccessTokenCommandHandler> logger,
            IGitHubPersonalAccessTokenInspector personalAccessTokenInspector)
        {
            this.writableSecretStore = writableSecretStore;
            this.validator = validator;
            this.logger = logger;
            this.personalAccessTokenInspector = personalAccessTokenInspector;
        }

        public async Task<ConfigureGitHubPersonalAccessTokenResultDto> Handle(ConfigureGitHubPersonalAccessTokenCommand request, CancellationToken cancellationToken)
        {
            await validator.ValidateAndThrowAsync(request, cancellationToken);

            string sanitizedToken = request.Token.Trim();
            GitHubPersonalAccessTokenInspectionResult inspection = await personalAccessTokenInspector.InspectAsync(
                sanitizedToken,
                GitHubPersonalAccessTokenRequirements.RequiredScopes,
                cancellationToken);

            if (!inspection.TokenAccepted)
            {
                string failureMessage = string.IsNullOrWhiteSpace(inspection.FailureReason)
                    ? "GitHub rechazó el token. Verifica que no haya expirado y que lo copiaste completo."
                    : inspection.FailureReason;

                List<ValidationFailure> failures = new List<ValidationFailure>
                {
                    new ValidationFailure(nameof(request.Token), failureMessage)
                };

                throw new ValidationException(failures);
            }

            if (!inspection.HasRequiredPermissions)
            {
                string message = CreateMissingPermissionsMessage(inspection);
                List<ValidationFailure> failures = new List<ValidationFailure>
                {
                    new ValidationFailure(nameof(request.Token), message)
                };

                throw new ValidationException(failures);
            }

            await writableSecretStore.SetSecretAsync("GitHubPersonalAccessToken", sanitizedToken, cancellationToken);

            logger.LogInformation("GitHub personal access token configured successfully for user {Login}.", inspection.Login ?? "unknown");

            GitHubPersonalAccessTokenValidationResultDto validation = GitHubPersonalAccessTokenValidationResultDto.FromInspection(inspection);
            ConfigureGitHubPersonalAccessTokenResultDto result = new ConfigureGitHubPersonalAccessTokenResultDto(true, validation);
            return result;
        }

        private static string CreateMissingPermissionsMessage(GitHubPersonalAccessTokenInspectionResult inspection)
        {
            if (inspection.MissingPermissions.Count > 0)
            {
                string joinedPermissions = string.Join(", ", inspection.MissingPermissions);
                return string.Concat("El token no incluye los permisos requeridos: ", joinedPermissions, ".");
            }

            if (!inspection.RepositoryAccessConfirmed)
            {
                return "No se pudo confirmar el acceso a repositorios privados. Revisa que el token tenga Contents: read o el scope repo.";
            }

            return "El token no cumple con los requisitos necesarios.";
        }
    }
}
