using System;

namespace MyApp.Domain.Observability
{
    public sealed class AuditTrailEntry
    {
        public AuditTrailEntry(Guid userId, string eventType, string provider, string payload, DateTimeOffset occurredAt, string correlationId)
        {
            if (userId == Guid.Empty)
            {
                throw new ArgumentException("The user identifier cannot be empty.", nameof(userId));
            }

            if (string.IsNullOrWhiteSpace(eventType))
            {
                throw new ArgumentException("The event type cannot be null or whitespace.", nameof(eventType));
            }

            if (string.IsNullOrWhiteSpace(provider))
            {
                throw new ArgumentException("The provider cannot be null or whitespace.", nameof(provider));
            }

            if (string.IsNullOrWhiteSpace(payload))
            {
                throw new ArgumentException("The payload cannot be null or whitespace.", nameof(payload));
            }

            Id = Guid.NewGuid();
            UserId = userId;
            EventType = eventType;
            Provider = provider;
            Payload = payload;
            OccurredAt = occurredAt;
            CorrelationId = correlationId ?? string.Empty;
        }

        private AuditTrailEntry()
        {
        }

        public Guid Id { get; private set; }

        public Guid UserId { get; private set; }

        public string EventType { get; private set; } = string.Empty;

        public string Provider { get; private set; } = string.Empty;

        public string Payload { get; private set; } = string.Empty;

        public DateTimeOffset OccurredAt { get; private set; }

        public string CorrelationId { get; private set; } = string.Empty;
    }
}
