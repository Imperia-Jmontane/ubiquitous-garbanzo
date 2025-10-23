namespace MyApp.Application.GitHubPersonalAccessToken.DTOs
{
    public sealed class ConfigureGitHubPersonalAccessTokenResultDto
    {
        public ConfigureGitHubPersonalAccessTokenResultDto(bool configured, GitHubPersonalAccessTokenValidationResultDto validation)
        {
            Configured = configured;
            Validation = validation;
        }

        public bool Configured { get; }

        public GitHubPersonalAccessTokenValidationResultDto Validation { get; }
    }
}
