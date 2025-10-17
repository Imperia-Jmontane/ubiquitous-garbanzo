using System;
using MyApp.Domain.ValueObjects;

namespace MyApp.Domain.Entities
{
    public sealed class GitHubAccountLink
    {
        public GitHubAccountLink(Guid userId, GitHubIdentity identity, string secretName, DateTimeOffset linkedOn, DateTimeOffset? lastRefreshed)
        {
            if (userId == Guid.Empty)
            {
                throw new ArgumentException("The user identifier is required.", nameof(userId));
            }

            if (identity == null)
            {
                throw new ArgumentNullException(nameof(identity));
            }

            if (string.IsNullOrWhiteSpace(secretName))
            {
                throw new ArgumentException("The secret name is required.", nameof(secretName));
            }

            UserId = userId;
            Identity = identity;
            SecretName = secretName;
            LinkedOn = linkedOn;
            LastRefreshed = lastRefreshed;
        }

        private GitHubAccountLink()
        {
            UserId = Guid.Empty;
            Identity = new GitHubIdentity(string.Empty, string.Empty, string.Empty, string.Empty);
            SecretName = string.Empty;
            LinkedOn = DateTimeOffset.MinValue;
            LastRefreshed = null;
        }

        public Guid UserId { get; private set; }

        public GitHubIdentity Identity { get; private set; }

        public string SecretName { get; private set; }

        public DateTimeOffset LinkedOn { get; private set; }

        public DateTimeOffset? LastRefreshed { get; private set; }

        public void Update(GitHubIdentity identity, string secretName, DateTimeOffset refreshedAt)
        {
            if (identity == null)
            {
                throw new ArgumentNullException(nameof(identity));
            }

            if (string.IsNullOrWhiteSpace(secretName))
            {
                throw new ArgumentException("The secret name is required.", nameof(secretName));
            }

            Identity = identity;
            SecretName = secretName;
            LastRefreshed = refreshedAt;
        }
    }
}
