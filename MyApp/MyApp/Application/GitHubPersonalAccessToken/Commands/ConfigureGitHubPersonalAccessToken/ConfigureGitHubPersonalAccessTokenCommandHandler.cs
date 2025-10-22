using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using MyApp.Application.Abstractions;
using MyApp.Application.GitHubPersonalAccessToken.DTOs;

namespace MyApp.Application.GitHubPersonalAccessToken.Commands.ConfigureGitHubPersonalAccessToken
{
    public sealed class ConfigureGitHubPersonalAccessTokenCommandHandler : IRequestHandler<ConfigureGitHubPersonalAccessTokenCommand, ConfigureGitHubPersonalAccessTokenResultDto>
    {
        private readonly IWritableSecretStore writableSecretStore;
        private readonly IValidator<ConfigureGitHubPersonalAccessTokenCommand> validator;
        private readonly ILogger<ConfigureGitHubPersonalAccessTokenCommandHandler> logger;

        public ConfigureGitHubPersonalAccessTokenCommandHandler(
            IWritableSecretStore writableSecretStore,
            IValidator<ConfigureGitHubPersonalAccessTokenCommand> validator,
            ILogger<ConfigureGitHubPersonalAccessTokenCommandHandler> logger)
        {
            this.writableSecretStore = writableSecretStore;
            this.validator = validator;
            this.logger = logger;
        }

        public async Task<ConfigureGitHubPersonalAccessTokenResultDto> Handle(ConfigureGitHubPersonalAccessTokenCommand request, CancellationToken cancellationToken)
        {
            await validator.ValidateAndThrowAsync(request, cancellationToken);

            string sanitizedToken = request.Token.Trim();
            await writableSecretStore.SetSecretAsync("GitHubPersonalAccessToken", sanitizedToken, cancellationToken);

            logger.LogInformation("GitHub personal access token configured successfully.");

            ConfigureGitHubPersonalAccessTokenResultDto result = new ConfigureGitHubPersonalAccessTokenResultDto(true);
            return result;
        }
    }
}
