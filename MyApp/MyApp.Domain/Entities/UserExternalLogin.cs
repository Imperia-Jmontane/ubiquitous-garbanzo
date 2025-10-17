using System;

namespace MyApp.Domain.Entities
{
    public sealed class UserExternalLogin
    {
        public UserExternalLogin(Guid id, Guid userId, string provider, string providerAccountId, string state, string secretName, DateTimeOffset createdAt)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentException("The identifier is required.", nameof(id));
            }

            if (userId == Guid.Empty)
            {
                throw new ArgumentException("The user identifier is required.", nameof(userId));
            }

            if (string.IsNullOrWhiteSpace(provider))
            {
                throw new ArgumentException("The provider name is required.", nameof(provider));
            }

            if (string.IsNullOrWhiteSpace(state))
            {
                throw new ArgumentException("The OAuth state is required.", nameof(state));
            }

            Id = id;
            UserId = userId;
            Provider = provider;
            ProviderAccountId = providerAccountId ?? string.Empty;
            State = state;
            SecretName = secretName ?? string.Empty;
            CreatedAt = createdAt;
            CompletedAt = null;
        }

        private UserExternalLogin()
        {
            Id = Guid.Empty;
            UserId = Guid.Empty;
            Provider = string.Empty;
            ProviderAccountId = string.Empty;
            State = string.Empty;
            SecretName = string.Empty;
            CreatedAt = DateTimeOffset.MinValue;
            CompletedAt = null;
        }

        public Guid Id { get; }

        public Guid UserId { get; }

        public string Provider { get; }

        public string ProviderAccountId { get; private set; }

        public string State { get; }

        public string SecretName { get; private set; }

        public DateTimeOffset CreatedAt { get; }

        public DateTimeOffset? CompletedAt { get; private set; }

        public void MarkCompleted(string providerAccountId, string secretName, DateTimeOffset completedAt)
        {
            ProviderAccountId = providerAccountId ?? string.Empty;
            SecretName = secretName ?? string.Empty;
            CompletedAt = completedAt;
        }
    }
}
