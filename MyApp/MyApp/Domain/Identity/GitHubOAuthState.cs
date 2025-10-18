using System;

namespace MyApp.Domain.Identity
{
    public sealed class GitHubOAuthState
    {
        public GitHubOAuthState(Guid userId, string state, string redirectUri, DateTimeOffset expiresAt)
        {
            if (userId == Guid.Empty)
            {
                throw new ArgumentException("The user identifier cannot be empty.", nameof(userId));
            }

            if (string.IsNullOrWhiteSpace(state))
            {
                throw new ArgumentException("The state cannot be null or whitespace.", nameof(state));
            }

            if (string.IsNullOrWhiteSpace(redirectUri))
            {
                throw new ArgumentException("The redirect URI cannot be null or whitespace.", nameof(redirectUri));
            }

            if (expiresAt <= DateTimeOffset.UtcNow)
            {
                throw new ArgumentException("The expiration time must be in the future.", nameof(expiresAt));
            }

            Id = Guid.NewGuid();
            UserId = userId;
            State = state;
            RedirectUri = redirectUri;
            ExpiresAt = expiresAt;
            CreatedAt = DateTimeOffset.UtcNow;
        }

        private GitHubOAuthState()
        {
        }

        public Guid Id { get; private set; }

        public Guid UserId { get; private set; }

        public string State { get; private set; } = string.Empty;

        public string RedirectUri { get; private set; } = string.Empty;

        public DateTimeOffset ExpiresAt { get; private set; }

        public DateTimeOffset CreatedAt { get; private set; }

        public bool IsExpired(DateTimeOffset referenceTime)
        {
            return ExpiresAt <= referenceTime;
        }
    }
}
