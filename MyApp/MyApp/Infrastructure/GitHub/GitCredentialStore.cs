using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MyApp.Application.Abstractions;
using MyApp.Application.GitHubOAuth.Models;
using MyApp.Infrastructure.Secrets;

namespace MyApp.Infrastructure.GitHub
{
    public sealed class GitCredentialStore : IGitCredentialStore
    {
        private readonly ISecretProvider secretProvider;
        private readonly ILogger<GitCredentialStore> logger;

        public GitCredentialStore(ISecretProvider secretProvider, ILogger<GitCredentialStore> logger)
        {
            this.secretProvider = secretProvider;
            this.logger = logger;
        }

        public async Task<GitHubOAuthClientCredentials> GetClientCredentialsAsync(CancellationToken cancellationToken)
        {
            string? clientId = await secretProvider.GetSecretAsync("GitHubClientId", cancellationToken);
            string? clientSecret = await secretProvider.GetSecretAsync("GitHubClientSecret", cancellationToken);

            if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
            {
                logger.LogError("GitHub OAuth credentials are missing from the secret store.");
                throw new InvalidOperationException("GitHub OAuth credentials were not found in the secret store.");
            }

            return new GitHubOAuthClientCredentials(clientId, clientSecret);
        }
    }
}
