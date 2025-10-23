using MediatR;
using MyApp.Application.GitHubPersonalAccessToken.DTOs;

namespace MyApp.Application.GitHubPersonalAccessToken.Commands.ConfigureGitHubPersonalAccessToken
{
    public sealed class ConfigureGitHubPersonalAccessTokenCommand : IRequest<ConfigureGitHubPersonalAccessTokenResultDto>
    {
        public ConfigureGitHubPersonalAccessTokenCommand(string token)
        {
            Token = token;
        }

        public string Token { get; }
    }
}
