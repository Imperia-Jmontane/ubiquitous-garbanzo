using System;
using System.Collections.Generic;

namespace MyApp.Domain.Tokens
{
    public sealed class GitHubToken
    {
        public GitHubToken(string value, DateTimeOffset issuedAt, DateTimeOffset expiresAt, IEnumerable<string> scopes)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("The token value cannot be null or whitespace.", nameof(value));
            }

            if (expiresAt <= issuedAt)
            {
                throw new ArgumentException("The expiration time must be greater than the issued time.", nameof(expiresAt));
            }

            if (scopes == null)
            {
                throw new ArgumentNullException(nameof(scopes));
            }

            List<string> normalizedScopes = new List<string>();
            foreach (string scope in scopes)
            {
                if (string.IsNullOrWhiteSpace(scope))
                {
                    throw new ArgumentException("A scope value cannot be null or whitespace.", nameof(scopes));
                }

                normalizedScopes.Add(scope.Trim());
            }

            if (normalizedScopes.Count == 0)
            {
                throw new ArgumentException("At least one scope must be provided for the GitHub token.", nameof(scopes));
            }

            Value = value;
            IssuedAt = issuedAt;
            ExpiresAt = expiresAt;
            Scopes = normalizedScopes.AsReadOnly();
        }

        public string Value { get; }

        public DateTimeOffset IssuedAt { get; }

        public DateTimeOffset ExpiresAt { get; }

        public IReadOnlyCollection<string> Scopes { get; }

        public bool HasScope(string requiredScope)
        {
            if (string.IsNullOrWhiteSpace(requiredScope))
            {
                throw new ArgumentException("The required scope cannot be null or whitespace.", nameof(requiredScope));
            }

            foreach (string scope in Scopes)
            {
                if (string.Equals(scope, requiredScope, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
