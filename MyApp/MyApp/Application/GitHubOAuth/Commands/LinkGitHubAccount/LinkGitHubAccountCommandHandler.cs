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

namespace MyApp.Application.GitHubOAuth.Commands.LinkGitHubAccount
{
    public sealed class LinkGitHubAccountCommandHandler : IRequestHandler<LinkGitHubAccountCommand, LinkGitHubAccountResultDto>
    {
        private const string ProviderName = "GitHub";
        private readonly IGitHubOAuthClient gitHubOAuthClient;
        private readonly IUserExternalLoginRepository userExternalLoginRepository;
        private readonly ISystemClock systemClock;
        private readonly IValidator<LinkGitHubAccountCommand> validator;
        private readonly ILogger<LinkGitHubAccountCommandHandler> logger;
        private readonly Counter<int> linkSuccessCounter;
        private readonly Counter<int> linkFailureCounter;

        public LinkGitHubAccountCommandHandler(
            IGitHubOAuthClient gitHubOAuthClient,
            IUserExternalLoginRepository userExternalLoginRepository,
            ISystemClock systemClock,
            IValidator<LinkGitHubAccountCommand> validator,
            ILogger<LinkGitHubAccountCommandHandler> logger,
            Meter meter)
        {
            this.gitHubOAuthClient = gitHubOAuthClient;
            this.userExternalLoginRepository = userExternalLoginRepository;
            this.systemClock = systemClock;
            this.validator = validator;
            this.logger = logger;
            linkSuccessCounter = meter.CreateCounter<int>("github.oauth.link.success.count");
            linkFailureCounter = meter.CreateCounter<int>("github.oauth.link.failure.count");
        }

        public async Task<LinkGitHubAccountResultDto> Handle(LinkGitHubAccountCommand request, CancellationToken cancellationToken)
        {
            await validator.ValidateAndThrowAsync(request, cancellationToken);

            try
            {
                GitHubCodeExchangeRequest exchangeRequest = new GitHubCodeExchangeRequest(request.Code, request.RedirectUri, request.State);
                GitHubOAuthTokenResponse response = await gitHubOAuthClient.ExchangeCodeAsync(exchangeRequest, cancellationToken);

                DateTimeOffset expiresAt = systemClock.UtcNow.Add(response.ExpiresIn);
                UserExternalLogin? existing = await userExternalLoginRepository.GetAsync(request.UserId, ProviderName, cancellationToken);

                bool created = false;
                if (existing == null)
                {
                    Guid externalUserId = Guid.NewGuid();
                    UserExternalLogin newLogin = new UserExternalLogin(request.UserId, ProviderName, response.ExternalUserId ?? externalUserId.ToString(), response.AccessToken, response.RefreshToken, expiresAt);
                    await userExternalLoginRepository.AddAsync(newLogin, cancellationToken);
                    created = true;
                }
                else
                {
                    existing.UpdateTokens(response.AccessToken, response.RefreshToken, expiresAt);
                    await userExternalLoginRepository.UpdateAsync(existing, cancellationToken);
                }

                linkSuccessCounter.Add(1);
                logger.LogInformation("GitHub account linked successfully for user {UserId}. NewLink: {IsNewConnection}", request.UserId, created);

                return new LinkGitHubAccountResultDto(request.UserId, ProviderName, response.Scopes, expiresAt, created);
            }
            catch (ValidationException)
            {
                linkFailureCounter.Add(1);
                throw;
            }
            catch (Exception exception)
            {
                linkFailureCounter.Add(1);
                logger.LogError(exception, "Failed to link GitHub account for user {UserId}", request.UserId);
                throw;
            }
        }
    }
}
