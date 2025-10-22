using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using MyApp.Application.GitHubPersonalAccessToken.DTOs;
using MyApp.Application.Abstractions;

namespace MyApp.Application.GitHubPersonalAccessToken.Queries.GetGitHubPersonalAccessTokenStatus
{
    public sealed class GetGitHubPersonalAccessTokenStatusQueryHandler : IRequestHandler<GetGitHubPersonalAccessTokenStatusQuery, GitHubPersonalAccessTokenStatusDto>
    {
        private readonly ISecretProvider secretProvider;

        public GetGitHubPersonalAccessTokenStatusQueryHandler(ISecretProvider secretProvider)
        {
            this.secretProvider = secretProvider;
        }

        public async Task<GitHubPersonalAccessTokenStatusDto> Handle(GetGitHubPersonalAccessTokenStatusQuery request, CancellationToken cancellationToken)
        {
            string? token = await secretProvider.GetSecretAsync("GitHubPersonalAccessToken", cancellationToken);
            bool isConfigured = !string.IsNullOrWhiteSpace(token);

            List<string> permissions = new List<string>
            {
                "repo (PAT clásico) o Contents: read en un PAT con permisos afinados",
                "workflow (PAT clásico) o Actions: read & write",
                "read:org si necesitas repos privados de organizaciones"
            };

            GitHubPersonalAccessTokenStatusDto dto = new GitHubPersonalAccessTokenStatusDto(
                isConfigured,
                "https://github.com/settings/tokens",
                permissions);

            return dto;
        }
    }
}
