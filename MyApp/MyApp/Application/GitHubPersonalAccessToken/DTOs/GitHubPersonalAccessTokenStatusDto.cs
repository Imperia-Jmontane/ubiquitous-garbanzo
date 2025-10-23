using System.Collections.Generic;

namespace MyApp.Application.GitHubPersonalAccessToken.DTOs
{
    public sealed class GitHubPersonalAccessTokenStatusDto
    {
        public GitHubPersonalAccessTokenStatusDto(
            bool tokenStored,
            bool isConfigured,
            string generationUrl,
            IReadOnlyCollection<string> requiredPermissions,
            GitHubPersonalAccessTokenValidationResultDto? validation)
        {
            TokenStored = tokenStored;
            IsConfigured = isConfigured;
            GenerationUrl = generationUrl;
            RequiredPermissions = requiredPermissions;
            Validation = validation;
        }

        public bool TokenStored { get; }

        public bool IsConfigured { get; }

        public string GenerationUrl { get; }

        public IReadOnlyCollection<string> RequiredPermissions { get; }

        public GitHubPersonalAccessTokenValidationResultDto? Validation { get; }
    }
}
