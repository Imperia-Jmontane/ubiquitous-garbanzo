using System.Collections.Generic;

namespace MyApp.Application.GitHubOAuth.DTOs
{
    public sealed class GitHubOAuthStatusDto
    {
        public GitHubOAuthStatusDto(bool isConfigured, string clientId, IReadOnlyList<string> scopes)
        {
            IsConfigured = isConfigured;
            ClientId = clientId;
            Scopes = scopes;
        }

        public bool IsConfigured { get; }

        public string ClientId { get; }

        public IReadOnlyList<string> Scopes { get; }
    }
}
