using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using MyApp.Application.Abstractions;
using MyApp.Domain.Observability;

namespace MyApp.Application.GitHubOAuth.Events
{
    public sealed class GitHubAccountLinkedEventHandler : INotificationHandler<GitHubAccountLinkedEvent>
    {
        private readonly IAuditTrailRepository auditTrailRepository;
        private readonly ILogger<GitHubAccountLinkedEventHandler> logger;

        public GitHubAccountLinkedEventHandler(IAuditTrailRepository auditTrailRepository, ILogger<GitHubAccountLinkedEventHandler> logger)
        {
            this.auditTrailRepository = auditTrailRepository;
            this.logger = logger;
        }

        public async Task Handle(GitHubAccountLinkedEvent notification, CancellationToken cancellationToken)
        {
            try
            {
                GitHubAccountLinkedAuditPayload payload = new GitHubAccountLinkedAuditPayload
                {
                    Scopes = notification.Scopes,
                    IsNewConnection = notification.IsNewConnection,
                    CanClone = notification.CanClone
                };

                string serializedPayload = JsonSerializer.Serialize(payload);
                AuditTrailEntry entry = new AuditTrailEntry(notification.UserId, "GitHubAccountLinked", notification.Provider, serializedPayload, notification.OccurredAt, notification.CorrelationId);

                await auditTrailRepository.AddAsync(entry, cancellationToken);

                logger.LogInformation("Audit entry stored for GitHub account link. UserId: {UserId}", notification.UserId);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Failed to persist the audit trail for user {UserId}", notification.UserId);
                throw;
            }
        }

        private sealed class GitHubAccountLinkedAuditPayload
        {
            public IReadOnlyCollection<string> Scopes { get; set; } = Array.Empty<string>();

            public bool IsNewConnection { get; set; }

            public bool CanClone { get; set; }
        }
    }
}
