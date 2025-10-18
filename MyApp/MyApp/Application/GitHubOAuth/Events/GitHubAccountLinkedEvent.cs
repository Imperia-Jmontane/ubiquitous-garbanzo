using System;
using System.Collections.Generic;
using MediatR;

namespace MyApp.Application.GitHubOAuth.Events
{
    public sealed class GitHubAccountLinkedEvent : INotification
    {
        public GitHubAccountLinkedEvent(Guid userId, string provider, IReadOnlyCollection<string> scopes, bool isNewConnection, bool canClone, DateTimeOffset occurredAt, string correlationId)
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
            IsNewConnection = isNewConnection;
            CanClone = canClone;
            OccurredAt = occurredAt;
            CorrelationId = correlationId;
        }

        public Guid UserId { get; }

        public string Provider { get; }

        public IReadOnlyCollection<string> Scopes { get; }

        public bool IsNewConnection { get; }

        public bool CanClone { get; }

        public DateTimeOffset OccurredAt { get; }

        public string CorrelationId { get; }
    }
}
