using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using MyApp.Application.GitHubOAuth.Configuration;
using MyApp.Application.GitHubOAuth.DTOs;

namespace MyApp.Application.GitHubOAuth.Queries.GetGitHubOAuthStatus
{
    public sealed class GetGitHubOAuthStatusQueryHandler : IRequestHandler<GetGitHubOAuthStatusQuery, GitHubOAuthStatusDto>
    {
        private readonly IGitHubOAuthSettingsProvider settingsProvider;

        public GetGitHubOAuthStatusQueryHandler(IGitHubOAuthSettingsProvider settingsProvider)
        {
            this.settingsProvider = settingsProvider;
        }

        public async Task<GitHubOAuthStatusDto> Handle(GetGitHubOAuthStatusQuery request, CancellationToken cancellationToken)
        {
            GitHubOAuthSettings settings = await settingsProvider.GetSettingsAsync(cancellationToken);
            List<string> scopes = new List<string>();
            foreach (string scope in settings.Scopes)
            {
                scopes.Add(scope);
            }

            GitHubOAuthStatusDto dto = new GitHubOAuthStatusDto(settings.IsConfigured, settings.ClientId, scopes);
            return dto;
        }
    }
}
