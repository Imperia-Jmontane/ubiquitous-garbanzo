namespace MyApp.Infrastructure.GitHub
{
    public sealed class GitHubOAuthOptions
    {
        public string TokenEndpoint { get; set; } = "https://github.com/login/oauth/access_token";
    }
}
