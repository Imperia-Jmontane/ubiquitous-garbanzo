using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using MyApp.Application.Abstractions;
using MyApp.Application.GitHubPersonalAccessToken;
using MyApp.Application.GitHubPersonalAccessToken.DTOs;
using MyApp.Application.GitHubPersonalAccessToken.Models;

namespace MyApp.Application.GitHubPersonalAccessToken.Queries.GetGitHubPersonalAccessTokenStatus
{
    public sealed class GetGitHubPersonalAccessTokenStatusQueryHandler : IRequestHandler<GetGitHubPersonalAccessTokenStatusQuery, GitHubPersonalAccessTokenStatusDto>
    {
        private readonly ISecretProvider secretProvider;
        private readonly IGitHubPersonalAccessTokenInspector personalAccessTokenInspector;

        public GetGitHubPersonalAccessTokenStatusQueryHandler(ISecretProvider secretProvider, IGitHubPersonalAccessTokenInspector personalAccessTokenInspector)
        {
            this.secretProvider = secretProvider;
            this.personalAccessTokenInspector = personalAccessTokenInspector;
        }

        public async Task<GitHubPersonalAccessTokenStatusDto> Handle(GetGitHubPersonalAccessTokenStatusQuery request, CancellationToken cancellationToken)
        {
            string? token = await secretProvider.GetSecretAsync("GitHubPersonalAccessToken", cancellationToken);
            bool tokenStored = !string.IsNullOrWhiteSpace(token);
            GitHubPersonalAccessTokenValidationResultDto? validation = null;
            bool isConfigured = false;

            if (tokenStored)
            {
                GitHubPersonalAccessTokenInspectionResult inspection = await personalAccessTokenInspector.InspectAsync(
                    token!,
                    GitHubPersonalAccessTokenRequirements.RequiredScopes,
                    cancellationToken);

                validation = GitHubPersonalAccessTokenValidationResultDto.FromInspection(inspection);
                isConfigured = inspection.TokenAccepted && inspection.HasRequiredPermissions;
            }

            List<string> permissions = new List<string>(GitHubPersonalAccessTokenRequirements.DisplayPermissions);

            GitHubPersonalAccessTokenStatusDto dto = new GitHubPersonalAccessTokenStatusDto(
                tokenStored,
                isConfigured,
                GitHubPersonalAccessTokenRequirements.GenerationUrl,
                permissions,
                validation);

            return dto;
        }
    }
}
