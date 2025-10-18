using MediatR;
using MyApp.Application.GitHubOAuth.DTOs;

namespace MyApp.Application.GitHubOAuth.Commands.ConfigureGitHubOAuthSecrets
{
    public sealed class ConfigureGitHubOAuthSecretsCommand : IRequest<ConfigureGitHubOAuthSecretsResultDto>
    {
        public ConfigureGitHubOAuthSecretsCommand(string clientId, string clientSecret, string setupPassword)
        {
            ClientId = clientId;
            ClientSecret = clientSecret;
            SetupPassword = setupPassword;
        }

        public string ClientId { get; }

        public string ClientSecret { get; }

        public string SetupPassword { get; }
    }
}
