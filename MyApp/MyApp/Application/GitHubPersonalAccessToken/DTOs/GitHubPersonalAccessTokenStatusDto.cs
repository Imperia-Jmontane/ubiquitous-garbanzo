using System.Collections.Generic;

namespace MyApp.Application.GitHubPersonalAccessToken.DTOs
{
    public sealed class GitHubPersonalAccessTokenStatusDto
    {
        public GitHubPersonalAccessTokenStatusDto(bool isConfigured, string generationUrl, IReadOnlyCollection<string> requiredPermissions)
        {
            IsConfigured = isConfigured;
            GenerationUrl = generationUrl;
            RequiredPermissions = requiredPermissions;
        }

        public bool IsConfigured { get; }

        public string GenerationUrl { get; }

        public IReadOnlyCollection<string> RequiredPermissions { get; }
    }
}
