namespace MyApp.Application.GitHubOAuth.DTOs
{
    public sealed class ConfigureGitHubOAuthSecretsResultDto
    {
        public ConfigureGitHubOAuthSecretsResultDto(bool configured)
        {
            Configured = configured;
        }

        public bool Configured { get; }
    }
}
