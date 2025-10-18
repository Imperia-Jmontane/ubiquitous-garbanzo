using System;

namespace MyApp.Domain.Identity
{
    public sealed class UserExternalLogin
    {
        public UserExternalLogin(Guid userId, string provider, string externalUserId, string accessToken, string? refreshToken, DateTimeOffset expiresAt)
        {
            if (userId == Guid.Empty)
            {
                throw new ArgumentException("The user identifier cannot be empty.", nameof(userId));
            }

            if (string.IsNullOrWhiteSpace(provider))
            {
                throw new ArgumentException("The external provider name cannot be null or whitespace.", nameof(provider));
            }

            if (string.IsNullOrWhiteSpace(externalUserId))
            {
                throw new ArgumentException("The external user identifier cannot be null or whitespace.", nameof(externalUserId));
            }

            if (string.IsNullOrWhiteSpace(accessToken))
            {
                throw new ArgumentException("The access token cannot be null or whitespace.", nameof(accessToken));
            }

            if (expiresAt <= DateTimeOffset.UtcNow.AddMinutes(-5))
            {
                throw new ArgumentException("The expiration time must be in the future.", nameof(expiresAt));
            }

            Id = Guid.NewGuid();
            UserId = userId;
            Provider = provider;
            ExternalUserId = externalUserId;
            AccessToken = accessToken;
            RefreshToken = string.IsNullOrWhiteSpace(refreshToken) ? null : refreshToken;
            ExpiresAt = expiresAt;
            CreatedAt = DateTimeOffset.UtcNow;
            UpdatedAt = CreatedAt;
        }

        private UserExternalLogin()
        {
        }

        public Guid Id { get; private set; }

        public Guid UserId { get; private set; }

        public string Provider { get; private set; } = string.Empty;

        public string ExternalUserId { get; private set; } = string.Empty;

        public string AccessToken { get; private set; } = string.Empty;

        public string? RefreshToken { get; private set; }

        public DateTimeOffset ExpiresAt { get; private set; }

        public DateTimeOffset CreatedAt { get; private set; }

        public DateTimeOffset UpdatedAt { get; private set; }

        public bool SupportsRefresh => !string.IsNullOrWhiteSpace(RefreshToken);

        public void UpdateTokens(string newAccessToken, string? newRefreshToken, DateTimeOffset newExpiresAt)
        {
            if (string.IsNullOrWhiteSpace(newAccessToken))
            {
                throw new ArgumentException("The new access token cannot be null or whitespace.", nameof(newAccessToken));
            }

            if (newExpiresAt <= DateTimeOffset.UtcNow.AddMinutes(-5))
            {
                throw new ArgumentException("The new expiration time must be in the future.", nameof(newExpiresAt));
            }

            AccessToken = newAccessToken;
            RefreshToken = string.IsNullOrWhiteSpace(newRefreshToken) ? null : newRefreshToken;
            ExpiresAt = newExpiresAt;
            UpdatedAt = DateTimeOffset.UtcNow;
        }
    }
}
