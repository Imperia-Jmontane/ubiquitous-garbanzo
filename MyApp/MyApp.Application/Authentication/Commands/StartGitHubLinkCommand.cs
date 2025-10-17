using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using MyApp.Application.Authentication.Interfaces;
using MyApp.Application.Authentication.Models;
using MyApp.Application.Common.Interfaces;
using MyApp.Domain.Entities;

namespace MyApp.Application.Authentication.Commands
{
    public sealed class StartGitHubLinkCommand : IRequest<GitHubAuthorizationInfo>
    {
        public StartGitHubLinkCommand(Guid userId, string redirectUri)
        {
            UserId = userId;
            RedirectUri = redirectUri;
        }

        public Guid UserId { get; }

        public string RedirectUri { get; }
    }

    public sealed class StartGitHubLinkCommandHandler : IRequestHandler<StartGitHubLinkCommand, GitHubAuthorizationInfo>
    {
        private const string ProviderName = "GitHub";

        private readonly IUserExternalLoginRepository externalLoginRepository;
        private readonly IGitHubOAuthClient gitHubOAuthClient;
        private readonly IStateGenerator stateGenerator;
        private readonly IDateTimeProvider dateTimeProvider;
        private readonly ILogger<StartGitHubLinkCommandHandler> logger;

        public StartGitHubLinkCommandHandler(
            IUserExternalLoginRepository externalLoginRepository,
            IGitHubOAuthClient gitHubOAuthClient,
            IStateGenerator stateGenerator,
            IDateTimeProvider dateTimeProvider,
            ILogger<StartGitHubLinkCommandHandler> logger)
        {
            this.externalLoginRepository = externalLoginRepository;
            this.gitHubOAuthClient = gitHubOAuthClient;
            this.stateGenerator = stateGenerator;
            this.dateTimeProvider = dateTimeProvider;
            this.logger = logger;
        }

        public async Task<GitHubAuthorizationInfo> Handle(StartGitHubLinkCommand request, CancellationToken cancellationToken)
        {
            string state = stateGenerator.CreateState(request.UserId);
            GitHubAuthorizationInfo authorizationInfo = gitHubOAuthClient.CreateAuthorizationInfo(state, request.RedirectUri);

            UserExternalLogin externalLogin = new UserExternalLogin(Guid.NewGuid(), request.UserId, ProviderName, string.Empty, state, string.Empty, dateTimeProvider.UtcNow);

            await externalLoginRepository.AddAsync(externalLogin, cancellationToken);
            await externalLoginRepository.SaveChangesAsync(cancellationToken);

            logger.LogInformation("GitHub link started for user {UserId} with state {State}.", request.UserId, state);

            return authorizationInfo;
        }
    }
}
