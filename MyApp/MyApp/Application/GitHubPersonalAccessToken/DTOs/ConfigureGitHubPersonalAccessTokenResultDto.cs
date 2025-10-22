namespace MyApp.Application.GitHubPersonalAccessToken.DTOs
{
    public sealed class ConfigureGitHubPersonalAccessTokenResultDto
    {
        public ConfigureGitHubPersonalAccessTokenResultDto(bool configured)
        {
            Configured = configured;
        }

        public bool Configured { get; }
    }
}
