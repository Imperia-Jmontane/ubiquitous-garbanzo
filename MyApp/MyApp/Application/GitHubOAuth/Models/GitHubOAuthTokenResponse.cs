using System;
using System.Collections.Generic;

namespace MyApp.Application.GitHubOAuth.Models
{
    public sealed class GitHubOAuthTokenResponse
    {
        public GitHubOAuthTokenResponse(string accessToken, string refreshToken, int expiresInSeconds, string tokenType, string scope, string? externalUserId)
        {
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                throw new ArgumentException("The access token cannot be null or whitespace.", nameof(accessToken));
            }

            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                throw new ArgumentException("The refresh token cannot be null or whitespace.", nameof(refreshToken));
            }

            if (expiresInSeconds <= 0)
            {
                throw new ArgumentException("The expiration interval must be positive.", nameof(expiresInSeconds));
            }

            if (string.IsNullOrWhiteSpace(tokenType))
            {
                throw new ArgumentException("The token type cannot be null or whitespace.", nameof(tokenType));
            }

            AccessToken = accessToken;
            RefreshToken = refreshToken;
            ExpiresIn = TimeSpan.FromSeconds(expiresInSeconds);
            TokenType = tokenType;
            Scopes = ParseScopes(scope);
            ExternalUserId = externalUserId;
        }

        public string AccessToken { get; }

        public string RefreshToken { get; }

        public TimeSpan ExpiresIn { get; }

        public string TokenType { get; }

        public IReadOnlyCollection<string> Scopes { get; }

        public string? ExternalUserId { get; }

        private static IReadOnlyCollection<string> ParseScopes(string scope)
        {
            List<string> normalizedScopes = new List<string>();
            if (string.IsNullOrWhiteSpace(scope))
            {
                return normalizedScopes.AsReadOnly();
            }

            string[] segments = scope.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (string segment in segments)
            {
                normalizedScopes.Add(segment);
            }

            return normalizedScopes.AsReadOnly();
        }
    }
}
