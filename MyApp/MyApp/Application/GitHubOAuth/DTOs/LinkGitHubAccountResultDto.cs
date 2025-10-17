using System;
using System.Collections.Generic;

namespace MyApp.Application.GitHubOAuth.DTOs
{
    public sealed class LinkGitHubAccountResultDto
    {
        public LinkGitHubAccountResultDto(Guid userId, string provider, IReadOnlyCollection<string> scopes, DateTimeOffset expiresAt, bool isNewConnection)
        {
            if (userId == Guid.Empty)
            {
                throw new ArgumentException("The user identifier cannot be empty.", nameof(userId));
            }

            if (string.IsNullOrWhiteSpace(provider))
            {
                throw new ArgumentException("The provider cannot be null or whitespace.", nameof(provider));
            }

            if (scopes == null)
            {
                throw new ArgumentNullException(nameof(scopes));
            }

            UserId = userId;
            Provider = provider;
            Scopes = scopes;
            ExpiresAt = expiresAt;
            IsNewConnection = isNewConnection;
        }

        public Guid UserId { get; }

        public string Provider { get; }

        public IReadOnlyCollection<string> Scopes { get; }

        public DateTimeOffset ExpiresAt { get; }

        public bool IsNewConnection { get; }
    }
}
