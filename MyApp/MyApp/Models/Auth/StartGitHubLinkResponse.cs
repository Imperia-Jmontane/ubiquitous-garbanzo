using System.Collections.Generic;

namespace MyApp.Models.Auth
{
    public sealed class StartGitHubLinkResponse
    {
        public string AuthorizationUrl { get; set; } = string.Empty;

        public string State { get; set; } = string.Empty;

        public IList<string> Scopes { get; set; } = new List<string>();
    }
}
