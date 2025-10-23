using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using MyApp.Application.Abstractions;
using MyApp.Application.GitHubOAuth.Configuration;

namespace MyApp.Infrastructure.GitHub
{
    public sealed class GitHubOAuthSettingsProvider : IGitHubOAuthSettingsProvider
    {
        private readonly IOptionsMonitor<GitHubOAuthOptions> optionsMonitor;
        private readonly ISecretProvider secretProvider;

        public GitHubOAuthSettingsProvider(IOptionsMonitor<GitHubOAuthOptions> optionsMonitor, ISecretProvider secretProvider)
        {
            if (optionsMonitor == null)
            {
                throw new ArgumentNullException(nameof(optionsMonitor));
            }

            if (secretProvider == null)
            {
                throw new ArgumentNullException(nameof(secretProvider));
            }

            this.optionsMonitor = optionsMonitor;
            this.secretProvider = secretProvider;
        }

        public async Task<GitHubOAuthSettings> GetSettingsAsync(CancellationToken cancellationToken)
        {
            GitHubOAuthOptions options = optionsMonitor.CurrentValue;
            string? storedClientId = await secretProvider.GetSecretAsync("GitHubClientId", cancellationToken);
            string? storedClientSecret = await secretProvider.GetSecretAsync("GitHubClientSecret", cancellationToken);

            string effectiveClientId = storedClientId ?? string.Empty;
            if (string.IsNullOrWhiteSpace(effectiveClientId))
            {
                if (IsPlaceholder(options.ClientId))
                {
                    effectiveClientId = string.Empty;
                }
                else
                {
                    effectiveClientId = options.ClientId;
                }
            }

            bool isConfigured = !string.IsNullOrWhiteSpace(storedClientId) && !string.IsNullOrWhiteSpace(storedClientSecret);

            List<string> scopes = new List<string>();
            foreach (string scope in options.Scopes)
            {
                scopes.Add(scope);
            }

            GitHubOAuthSettings settings = new GitHubOAuthSettings(
                effectiveClientId ?? string.Empty,
                options.AuthorizationEndpoint,
                options.TokenEndpoint,
                options.UserInformationEndpoint,
                options.CallbackPath,
                scopes,
                isConfigured);

            return settings;
        }

        private static bool IsPlaceholder(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string trimmedValue = value.Trim();
            if (!trimmedValue.StartsWith("${", StringComparison.Ordinal))
            {
                return false;
            }

            return trimmedValue.EndsWith("}", StringComparison.Ordinal);
        }
    }
}
