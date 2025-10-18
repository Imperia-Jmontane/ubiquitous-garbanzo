using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyApp.Application.Abstractions;
using MyApp.Application.GitHubOAuth.Configuration;
using MyApp.Application.GitHubOAuth.DTOs;
using MyApp.Domain.Identity;
using MyApp.Domain.Scopes;

namespace MyApp.Application.GitHubOAuth.Commands.StartGitHubOAuth
{
    public sealed class StartGitHubOAuthCommandHandler : IRequestHandler<StartGitHubOAuthCommand, StartGitHubOAuthResultDto>
    {
        private readonly IGitHubOAuthStateRepository stateRepository;
        private readonly ISystemClock systemClock;
        private readonly IValidator<StartGitHubOAuthCommand> validator;
        private readonly ILogger<StartGitHubOAuthCommandHandler> logger;
        private readonly GitHubOAuthOptions options;

        public StartGitHubOAuthCommandHandler(
            IGitHubOAuthStateRepository stateRepository,
            ISystemClock systemClock,
            IValidator<StartGitHubOAuthCommand> validator,
            ILogger<StartGitHubOAuthCommandHandler> logger,
            IOptions<GitHubOAuthOptions> options)
        {
            this.stateRepository = stateRepository;
            this.systemClock = systemClock;
            this.validator = validator;
            this.logger = logger;
            this.options = options.Value;
        }

        public async Task<StartGitHubOAuthResultDto> Handle(StartGitHubOAuthCommand request, CancellationToken cancellationToken)
        {
            await validator.ValidateAndThrowAsync(request, cancellationToken);

            await stateRepository.RemoveExpiredAsync(systemClock.UtcNow, cancellationToken);

            DateTimeOffset issuedAt = systemClock.UtcNow;
            DateTimeOffset expiresAt = issuedAt.AddMinutes(10);
            string state = GenerateStateToken();

            GitHubOAuthState oauthState = new GitHubOAuthState(request.UserId, state, request.RedirectUri, expiresAt);
            await stateRepository.AddAsync(oauthState, cancellationToken);

            string scopeParameter = BuildScopeParameter(options.Scopes);
            string authorizationUrl = BuildAuthorizationUrl(request.RedirectUri, state, scopeParameter);
            bool canClone = MandatoryScopeSet.AreSatisfiedBy(options.Scopes);

            logger.LogInformation("GitHub OAuth start requested for user {UserId}. State: {State}", request.UserId, state);

            return new StartGitHubOAuthResultDto(request.UserId, authorizationUrl, state, options.Scopes, expiresAt, canClone);
        }

        private static string GenerateStateToken()
        {
            byte[] buffer = new byte[32];
            RandomNumberGenerator.Fill(buffer);
            return Convert.ToHexString(buffer);
        }

        private static string BuildScopeParameter(IReadOnlyCollection<string> scopes)
        {
            if (scopes == null || scopes.Count == 0)
            {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder();
            bool first = true;
            foreach (string scope in scopes)
            {
                if (string.IsNullOrWhiteSpace(scope))
                {
                    continue;
                }

                if (!first)
                {
                    builder.Append(' ');
                }

                builder.Append(scope.Trim());
                first = false;
            }

            return builder.ToString();
        }

        private string BuildAuthorizationUrl(string redirectUri, string state, string scopeParameter)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append(options.AuthorizationEndpoint);
            builder.Append("?client_id=");
            builder.Append(Uri.EscapeDataString(options.ClientId));
            builder.Append("&redirect_uri=");
            builder.Append(Uri.EscapeDataString(redirectUri));

            if (!string.IsNullOrWhiteSpace(scopeParameter))
            {
                builder.Append("&scope=");
                builder.Append(Uri.EscapeDataString(scopeParameter));
            }

            builder.Append("&state=");
            builder.Append(Uri.EscapeDataString(state));
            builder.Append("&allow_signup=false");

            return builder.ToString();
        }
    }
}
