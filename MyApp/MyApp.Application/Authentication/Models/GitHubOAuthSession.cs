using MyApp.Domain.ValueObjects;

namespace MyApp.Application.Authentication.Models
{
    public sealed class GitHubOAuthSession
    {
        public GitHubOAuthSession(GitHubIdentity identity, GitHubToken token)
        {
            Identity = identity;
            Token = token;
        }

        public GitHubIdentity Identity { get; }

        public GitHubToken Token { get; }
    }
}
