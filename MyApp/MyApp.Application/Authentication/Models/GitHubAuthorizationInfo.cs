using System.Collections.Generic;

namespace MyApp.Application.Authentication.Models
{
    public sealed class GitHubAuthorizationInfo
    {
        public GitHubAuthorizationInfo(string authorizationUrl, string state, IReadOnlyCollection<string> scopes)
        {
            AuthorizationUrl = authorizationUrl;
            State = state;
            Scopes = scopes;
        }

        public string AuthorizationUrl { get; }

        public string State { get; }

        public IReadOnlyCollection<string> Scopes { get; }
    }
}
