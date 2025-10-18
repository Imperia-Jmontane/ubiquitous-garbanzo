using System.Collections.Generic;

namespace MyApp.Application.GitHubOAuth.Configuration
{
    public sealed class GitHubOAuthOptions
    {
        public string ClientId { get; set; } = string.Empty;

        public string ClientSecret { get; set; } = string.Empty;

        public string AuthorizationEndpoint { get; set; } = "https://github.com/login/oauth/authorize";

        public string TokenEndpoint { get; set; } = "https://github.com/login/oauth/access_token";

        public string UserInformationEndpoint { get; set; } = "https://api.github.com/user";

        public string CallbackPath { get; set; } = "/signin-github";

        public List<string> Scopes { get; set; } = new List<string>();
    }
}
