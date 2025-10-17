using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using MyApp.Application.Authentication.Interfaces;
using MyApp.Application.Common.Interfaces;
using MyApp.Domain.Entities;
using MyApp.Domain.ValueObjects;

namespace MyApp.Application.Authentication.Commands
{
    public sealed class RefreshGitHubTokenCommand : IRequest<GitHubToken>
    {
        public RefreshGitHubTokenCommand(Guid userId)
        {
            UserId = userId;
        }

        public Guid UserId { get; }
    }

    public sealed class RefreshGitHubTokenCommandHandler : IRequestHandler<RefreshGitHubTokenCommand, GitHubToken>
    {
        private readonly IGitHubAccountLinkRepository accountLinkRepository;
        private readonly IGitCredentialStore credentialStore;
        private readonly IGitHubOAuthClient gitHubOAuthClient;
        private readonly IDateTimeProvider dateTimeProvider;
        private readonly ILogger<RefreshGitHubTokenCommandHandler> logger;

        public RefreshGitHubTokenCommandHandler(
            IGitHubAccountLinkRepository accountLinkRepository,
            IGitCredentialStore credentialStore,
            IGitHubOAuthClient gitHubOAuthClient,
            IDateTimeProvider dateTimeProvider,
            ILogger<RefreshGitHubTokenCommandHandler> logger)
        {
            this.accountLinkRepository = accountLinkRepository;
            this.credentialStore = credentialStore;
            this.gitHubOAuthClient = gitHubOAuthClient;
            this.dateTimeProvider = dateTimeProvider;
            this.logger = logger;
        }

        public async Task<GitHubToken> Handle(RefreshGitHubTokenCommand request, CancellationToken cancellationToken)
        {
            GitHubAccountLink? accountLink = await accountLinkRepository.GetByUserIdAsync(request.UserId, cancellationToken);

            if (accountLink == null)
            {
                throw new InvalidOperationException("The GitHub account is not linked.");
            }

            GitHubToken? existingToken = await credentialStore.GetAsync(accountLink.SecretName, cancellationToken);

            if (existingToken == null)
            {
                throw new InvalidOperationException("The GitHub credentials were not found.");
            }

            if (!existingToken.CanRefresh())
            {
                throw new InvalidOperationException("The GitHub credentials cannot be refreshed.");
            }

            logger.LogInformation("Refreshing GitHub token for user {UserId}.", request.UserId);

            GitHubToken refreshedToken = (await gitHubOAuthClient.RefreshTokenAsync(existingToken.RefreshToken, cancellationToken)).Token;

            await credentialStore.UpdateAsync(accountLink.SecretName, refreshedToken, cancellationToken);

            accountLink.Update(accountLink.Identity, accountLink.SecretName, dateTimeProvider.UtcNow);
            await accountLinkRepository.UpdateAsync(accountLink, cancellationToken);
            await accountLinkRepository.SaveChangesAsync(cancellationToken);

            return refreshedToken;
        }
    }
}
