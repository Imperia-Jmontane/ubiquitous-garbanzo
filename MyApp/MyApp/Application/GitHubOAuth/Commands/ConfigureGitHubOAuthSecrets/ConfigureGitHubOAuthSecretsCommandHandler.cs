using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyApp.Application.Abstractions;
using MyApp.Application.Configuration;
using MyApp.Application.GitHubOAuth.DTOs;
using MyApp.Domain.Observability;

namespace MyApp.Application.GitHubOAuth.Commands.ConfigureGitHubOAuthSecrets
{
    public sealed class ConfigureGitHubOAuthSecretsCommandHandler : IRequestHandler<ConfigureGitHubOAuthSecretsCommand, ConfigureGitHubOAuthSecretsResultDto>
    {
        private static readonly Guid DefaultAuditUserId = new Guid("d44a01be-e1bb-4ab0-9e02-4ed072d7228d");
        private readonly IWritableSecretStore writableSecretStore;
        private readonly IAuditTrailRepository auditTrailRepository;
        private readonly ISystemClock systemClock;
        private readonly IValidator<ConfigureGitHubOAuthSecretsCommand> validator;
        private readonly ILogger<ConfigureGitHubOAuthSecretsCommandHandler> logger;
        private readonly IOptions<BootstrapOptions> bootstrapOptions;

        public ConfigureGitHubOAuthSecretsCommandHandler(
            IWritableSecretStore writableSecretStore,
            IAuditTrailRepository auditTrailRepository,
            ISystemClock systemClock,
            IValidator<ConfigureGitHubOAuthSecretsCommand> validator,
            ILogger<ConfigureGitHubOAuthSecretsCommandHandler> logger,
            IOptions<BootstrapOptions> bootstrapOptions)
        {
            this.writableSecretStore = writableSecretStore;
            this.auditTrailRepository = auditTrailRepository;
            this.systemClock = systemClock;
            this.validator = validator;
            this.logger = logger;
            this.bootstrapOptions = bootstrapOptions;
        }

        public async Task<ConfigureGitHubOAuthSecretsResultDto> Handle(ConfigureGitHubOAuthSecretsCommand request, CancellationToken cancellationToken)
        {
            await validator.ValidateAndThrowAsync(request, cancellationToken);

            BootstrapOptions options = bootstrapOptions.Value;
            if (string.IsNullOrWhiteSpace(options.SetupPassword))
            {
                throw new InvalidOperationException("The bootstrap setup password has not been configured.");
            }

            if (!string.Equals(options.SetupPassword, request.SetupPassword, StringComparison.Ordinal))
            {
                logger.LogWarning("Bootstrap password validation failed while attempting to configure GitHub OAuth secrets.");
                throw new InvalidOperationException("The provided setup password is invalid.");
            }

            await writableSecretStore.SetSecretAsync("GitHubClientId", request.ClientId, cancellationToken);
            await writableSecretStore.SetSecretAsync("GitHubClientSecret", request.ClientSecret, cancellationToken);

            Guid auditUserId = options.AuditUserId == Guid.Empty ? DefaultAuditUserId : options.AuditUserId;
            DateTimeOffset occurredAt = systemClock.UtcNow;
            string correlationId = Activity.Current != null ? Activity.Current.TraceId.ToString() : string.Empty;

            string clientIdSuffix = request.ClientId.Length > 4 ? request.ClientId.Substring(request.ClientId.Length - 4) : request.ClientId;
            Dictionary<string, string> payloadValues = new Dictionary<string, string>
            {
                { "action", "configure" },
                { "clientIdSuffix", clientIdSuffix },
                { "occurredAt", occurredAt.ToString("O") }
            };

            string payload = JsonSerializer.Serialize(payloadValues);

            AuditTrailEntry auditEntry = new AuditTrailEntry(
                auditUserId,
                "GitHubOAuthSecretsConfigured",
                "GitHub",
                payload,
                occurredAt,
                correlationId);

            await auditTrailRepository.AddAsync(auditEntry, cancellationToken);

            logger.LogInformation("GitHub OAuth secrets configured successfully via bootstrap experience.");

            ConfigureGitHubOAuthSecretsResultDto result = new ConfigureGitHubOAuthSecretsResultDto(true);
            return result;
        }
    }
}
