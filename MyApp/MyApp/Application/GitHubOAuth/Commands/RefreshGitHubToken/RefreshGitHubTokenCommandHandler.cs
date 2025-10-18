using System;
using System.Diagnostics.Metrics;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using MyApp.Application.Abstractions;
using MyApp.Application.GitHubOAuth.DTOs;
using MyApp.Application.GitHubOAuth.Models;
using MyApp.Domain.Identity;
using MyApp.Domain.Scopes;

namespace MyApp.Application.GitHubOAuth.Commands.RefreshGitHubToken
{
    public sealed class RefreshGitHubTokenCommandHandler : IRequestHandler<RefreshGitHubTokenCommand, RefreshGitHubTokenResultDto>
    {
        private const string ProviderName = "GitHub";
        private readonly IGitHubOAuthClient gitHubOAuthClient;
        private readonly IUserExternalLoginRepository userExternalLoginRepository;
        private readonly ISystemClock systemClock;
        private readonly IValidator<RefreshGitHubTokenCommand> validator;
        private readonly ILogger<RefreshGitHubTokenCommandHandler> logger;
        private readonly Counter<int> refreshSuccessCounter;
        private readonly Counter<int> refreshFailureCounter;
        private readonly Counter<int> refreshExpiredCounter;

        public RefreshGitHubTokenCommandHandler(
            IGitHubOAuthClient gitHubOAuthClient,
            IUserExternalLoginRepository userExternalLoginRepository,
            ISystemClock systemClock,
            IValidator<RefreshGitHubTokenCommand> validator,
            ILogger<RefreshGitHubTokenCommandHandler> logger,
            Meter meter)
        {
            this.gitHubOAuthClient = gitHubOAuthClient;
            this.userExternalLoginRepository = userExternalLoginRepository;
            this.systemClock = systemClock;
            this.validator = validator;
            this.logger = logger;
            refreshSuccessCounter = meter.CreateCounter<int>("github.oauth.refresh.success.count");
            refreshFailureCounter = meter.CreateCounter<int>("github.oauth.refresh.failure.count");
            refreshExpiredCounter = meter.CreateCounter<int>("github.oauth.refresh.expired.count");
        }

        public async Task<RefreshGitHubTokenResultDto> Handle(RefreshGitHubTokenCommand request, CancellationToken cancellationToken)
        {
            await validator.ValidateAndThrowAsync(request, cancellationToken);

            try
            {
                UserExternalLogin? existing = await userExternalLoginRepository.GetAsync(request.UserId, ProviderName, cancellationToken);
                if (existing == null)
                {
                    throw new InvalidOperationException("No GitHub connection is registered for the user.");
                }

                bool wasExpired = existing.ExpiresAt <= systemClock.UtcNow;
                if (wasExpired)
                {
                    refreshExpiredCounter.Add(1);
                }

                GitHubTokenRefreshRequest refreshRequest = new GitHubTokenRefreshRequest(existing.RefreshToken);
                GitHubOAuthTokenResponse response = await gitHubOAuthClient.RefreshTokenAsync(refreshRequest, cancellationToken);

                DateTimeOffset expiresAt = systemClock.UtcNow.Add(response.ExpiresIn);
                existing.UpdateTokens(response.AccessToken, response.RefreshToken, expiresAt);
                await userExternalLoginRepository.UpdateAsync(existing, cancellationToken);

                refreshSuccessCounter.Add(1);
                logger.LogInformation("GitHub token refreshed for user {UserId}. WasExpired: {WasExpired}", request.UserId, wasExpired);

                bool canClone = MandatoryScopeSet.AreSatisfiedBy(response.Scopes);

                return new RefreshGitHubTokenResultDto(request.UserId, response.Scopes, expiresAt, wasExpired, canClone);
            }
            catch (ValidationException)
            {
                refreshFailureCounter.Add(1);
                throw;
            }
            catch (Exception exception)
            {
                refreshFailureCounter.Add(1);
                logger.LogError(exception, "Failed to refresh the GitHub token for user {UserId}", request.UserId);
                throw;
            }
        }
    }
}
