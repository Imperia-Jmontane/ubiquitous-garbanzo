using System;
using System.Collections.Generic;
using System.Linq;

namespace MyApp.Domain.ValueObjects
{
    public sealed class GitHubToken
    {
        private static readonly IReadOnlyCollection<string> RequiredScopes = new List<string> { "repo", "read:user" };

        public GitHubToken(string accessToken, string refreshToken, DateTimeOffset issuedAt, DateTimeOffset? expiresAt, IEnumerable<string> scopes)
        {
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                throw new ArgumentException("The GitHub access token is required.", nameof(accessToken));
            }

            AccessToken = accessToken;
            RefreshToken = string.IsNullOrWhiteSpace(refreshToken) ? string.Empty : refreshToken;
            IssuedAt = issuedAt;
            ExpiresAt = expiresAt;
            Scopes = scopes?.Distinct(StringComparer.OrdinalIgnoreCase).ToList() ?? new List<string>();

            ValidateRequiredScopes();
        }

        public string AccessToken { get; }

        public string RefreshToken { get; }

        public DateTimeOffset IssuedAt { get; }

        public DateTimeOffset? ExpiresAt { get; }

        public IReadOnlyCollection<string> Scopes { get; }

        public bool CanRefresh()
        {
            return !string.IsNullOrWhiteSpace(RefreshToken);
        }

        public bool HasExpired(DateTimeOffset now)
        {
            if (!ExpiresAt.HasValue)
            {
                return false;
            }

            return now >= ExpiresAt.Value;
        }

        public bool AllowsRepositoryClone()
        {
            return Scopes.Any(scope => string.Equals(scope, "repo", StringComparison.OrdinalIgnoreCase));
        }

        private void ValidateRequiredScopes()
        {
            foreach (string requiredScope in RequiredScopes)
            {
                bool scopePresent = Scopes.Any(scope => string.Equals(scope, requiredScope, StringComparison.OrdinalIgnoreCase));

                if (!scopePresent)
                {
                    throw new InvalidOperationException($"Missing required GitHub scope '{requiredScope}'.");
                }
            }
        }
    }
}
