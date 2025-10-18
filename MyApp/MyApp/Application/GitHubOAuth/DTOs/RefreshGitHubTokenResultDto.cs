using System;
using System.Collections.Generic;

namespace MyApp.Application.GitHubOAuth.DTOs
{
    public sealed class RefreshGitHubTokenResultDto
    {
        public RefreshGitHubTokenResultDto(Guid userId, IReadOnlyCollection<string> scopes, DateTimeOffset expiresAt, bool wasExpired, bool canClone)
        {
            if (userId == Guid.Empty)
            {
                throw new ArgumentException("The user identifier cannot be empty.", nameof(userId));
            }

            if (scopes == null)
            {
                throw new ArgumentNullException(nameof(scopes));
            }

            UserId = userId;
            Scopes = scopes;
            ExpiresAt = expiresAt;
            WasExpired = wasExpired;
            CanClone = canClone;
        }

        public Guid UserId { get; }

        public IReadOnlyCollection<string> Scopes { get; }

        public DateTimeOffset ExpiresAt { get; }

        public bool WasExpired { get; }

        public bool CanClone { get; }
    }
}
