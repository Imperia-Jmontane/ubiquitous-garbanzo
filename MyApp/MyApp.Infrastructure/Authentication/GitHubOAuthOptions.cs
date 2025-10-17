using System.Collections.Generic;

namespace MyApp.Infrastructure.Authentication
{
    public sealed class GitHubOAuthOptions
    {
        public string ClientId { get; set; } = string.Empty;

        public string ClientSecret { get; set; } = string.Empty;

        public string AuthorizationEndpoint { get; set; } = "https://github.com/login/oauth/authorize";

        public string TokenEndpoint { get; set; } = "https://github.com/login/oauth/access_token";

        public string UserEndpoint { get; set; } = "https://api.github.com/user";

        public IList<string> Scopes { get; set; } = new List<string> { "repo", "read:user" };

        public IList<string> AllowedRedirectUris { get; set; } = new List<string>();
    }
}
