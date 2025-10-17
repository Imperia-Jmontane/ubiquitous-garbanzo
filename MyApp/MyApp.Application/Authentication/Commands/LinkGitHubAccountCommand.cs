using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using MyApp.Application.Authentication.Interfaces;
using MyApp.Application.Authentication.Models;
using MyApp.Application.Common.Interfaces;
using MyApp.Domain.Entities;
using MyApp.Domain.Events;

namespace MyApp.Application.Authentication.Commands
{
    public sealed class LinkGitHubAccountCommand : IRequest<LinkGitHubAccountResult>
    {
        public LinkGitHubAccountCommand(Guid userId, string code, string state, string redirectUri)
        {
            UserId = userId;
            Code = code;
            State = state;
            RedirectUri = redirectUri;
        }

        public Guid UserId { get; }

        public string Code { get; }

        public string State { get; }

        public string RedirectUri { get; }
    }

    public sealed class LinkGitHubAccountResult
    {
        public LinkGitHubAccountResult(GitHubAccountLink accountLink, bool canClone)
        {
            AccountLink = accountLink;
            CanClone = canClone;
        }

        public GitHubAccountLink AccountLink { get; }

        public bool CanClone { get; }
    }

    public sealed class LinkGitHubAccountCommandHandler : IRequestHandler<LinkGitHubAccountCommand, LinkGitHubAccountResult>
    {
        private readonly IUserExternalLoginRepository externalLoginRepository;
        private readonly IGitHubAccountLinkRepository accountLinkRepository;
        private readonly IGitCredentialStore credentialStore;
        private readonly IGitHubOAuthClient gitHubOAuthClient;
        private readonly IDateTimeProvider dateTimeProvider;
        private readonly IPublisher publisher;
        private readonly IGitHubLinkMetrics metrics;
        private readonly ILogger<LinkGitHubAccountCommandHandler> logger;

        public LinkGitHubAccountCommandHandler(
            IUserExternalLoginRepository externalLoginRepository,
            IGitHubAccountLinkRepository accountLinkRepository,
            IGitCredentialStore credentialStore,
            IGitHubOAuthClient gitHubOAuthClient,
            IDateTimeProvider dateTimeProvider,
            IPublisher publisher,
            IGitHubLinkMetrics metrics,
            ILogger<LinkGitHubAccountCommandHandler> logger)
        {
            this.externalLoginRepository = externalLoginRepository;
            this.accountLinkRepository = accountLinkRepository;
            this.credentialStore = credentialStore;
            this.gitHubOAuthClient = gitHubOAuthClient;
            this.dateTimeProvider = dateTimeProvider;
            this.publisher = publisher;
            this.metrics = metrics;
            this.logger = logger;
        }

        public async Task<LinkGitHubAccountResult> Handle(LinkGitHubAccountCommand request, CancellationToken cancellationToken)
        {
            try
            {
                UserExternalLogin? externalLogin = await externalLoginRepository.FindByStateAsync(request.State, cancellationToken);

                if (externalLogin == null)
                {
                    throw new InvalidOperationException("The GitHub linking session was not found or has expired.");
                }

                if (externalLogin.UserId != request.UserId)
                {
                    throw new InvalidOperationException("The GitHub linking session does not belong to the authenticated user.");
                }

                logger.LogInformation("Exchanging GitHub code for user {UserId} with state {State}.", request.UserId, request.State);

                GitHubOAuthSession session = await gitHubOAuthClient.ExchangeCodeForTokenAsync(request.Code, request.RedirectUri, cancellationToken);

                string secretName = externalLogin.SecretName;

                if (string.IsNullOrWhiteSpace(secretName))
                {
                    secretName = await credentialStore.StoreAsync(request.UserId, session.Token, cancellationToken);
                }
                else
                {
                    await credentialStore.UpdateAsync(secretName, session.Token, cancellationToken);
                }

                externalLogin.MarkCompleted(session.Identity.AccountId, secretName, dateTimeProvider.UtcNow);
                await externalLoginRepository.SaveChangesAsync(cancellationToken);

                GitHubAccountLink? existingAccountLink = await accountLinkRepository.GetByUserIdAsync(request.UserId, cancellationToken);
                GitHubAccountLink accountLink;

                if (existingAccountLink == null)
                {
                    accountLink = new GitHubAccountLink(request.UserId, session.Identity, secretName, dateTimeProvider.UtcNow, dateTimeProvider.UtcNow);
                    await accountLinkRepository.AddAsync(accountLink, cancellationToken);
                }
                else
                {
                    existingAccountLink.Update(session.Identity, secretName, dateTimeProvider.UtcNow);
                    accountLink = existingAccountLink;
                    await accountLinkRepository.UpdateAsync(existingAccountLink, cancellationToken);
                }

                await accountLinkRepository.SaveChangesAsync(cancellationToken);

                logger.LogInformation("GitHub account {Login} linked for user {UserId}.", session.Identity.Login, request.UserId);

                GitHubAccountLinkedEvent domainEvent = new GitHubAccountLinkedEvent(accountLink);
                await publisher.Publish(domainEvent, cancellationToken);

                bool canClone = session.Token.AllowsRepositoryClone();
                metrics.RecordLinkSuccess(request.UserId);

                return new LinkGitHubAccountResult(accountLink, canClone);
            }
            catch (Exception exception)
            {
                metrics.RecordLinkFailure(request.UserId, exception.Message);
                logger.LogError(exception, "GitHub account linking failed for user {UserId} and state {State}.", request.UserId, request.State);
                throw;
            }
        }
    }
}
