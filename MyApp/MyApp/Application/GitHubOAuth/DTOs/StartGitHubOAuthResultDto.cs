using System;
using System.Collections.Generic;

namespace MyApp.Application.GitHubOAuth.DTOs
{
    public sealed class StartGitHubOAuthResultDto
    {
        public StartGitHubOAuthResultDto(Guid userId, string authorizationUrl, string state, IReadOnlyCollection<string> scopes, DateTimeOffset expiresAt, bool canClone)
        {
            if (userId == Guid.Empty)
            {
                throw new ArgumentException("The user identifier cannot be empty.", nameof(userId));
            }

            if (string.IsNullOrWhiteSpace(authorizationUrl))
            {
                throw new ArgumentException("The authorization URL cannot be null or whitespace.", nameof(authorizationUrl));
            }

            if (string.IsNullOrWhiteSpace(state))
            {
                throw new ArgumentException("The state cannot be null or whitespace.", nameof(state));
            }

            if (scopes == null)
            {
                throw new ArgumentNullException(nameof(scopes));
            }

            UserId = userId;
            AuthorizationUrl = authorizationUrl;
            State = state;
            Scopes = scopes;
            ExpiresAt = expiresAt;
            CanClone = canClone;
        }

        public Guid UserId { get; }

        public string AuthorizationUrl { get; }

        public string State { get; }

        public IReadOnlyCollection<string> Scopes { get; }

        public DateTimeOffset ExpiresAt { get; }

        public bool CanClone { get; }
    }
}
