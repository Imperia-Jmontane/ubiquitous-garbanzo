using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using MyApp.Application.Common.Interfaces;
using MyApp.Domain.ValueObjects;

namespace MyApp.Infrastructure.Security
{
    public sealed class GitCredentialStore : IGitCredentialStore
    {
        private readonly ISecretRepository secretRepository;
        private readonly IDataProtector dataProtector;
        private readonly GitCredentialStoreOptions options;
        private readonly IDateTimeProvider dateTimeProvider;
        private readonly JsonSerializerOptions serializerOptions;

        public GitCredentialStore(
            ISecretRepository secretRepository,
            IDataProtectionProvider dataProtectionProvider,
            IOptions<GitCredentialStoreOptions> options,
            IDateTimeProvider dateTimeProvider)
        {
            this.secretRepository = secretRepository;
            dataProtector = dataProtectionProvider.CreateProtector("github-token-store");
            this.options = options.Value;
            this.dateTimeProvider = dateTimeProvider;
            serializerOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };
        }

        public async Task<string> StoreAsync(Guid userId, GitHubToken token, CancellationToken cancellationToken)
        {
            string secretName = BuildSecretName(userId);
            string serializedToken = SerializeToken(token);
            string protectedToken = dataProtector.Protect(serializedToken);

            IDictionary<string, string> tags = new Dictionary<string, string>
            {
                { "userId", userId.ToString() },
                { "issuedAt", token.IssuedAt.ToString("O") }
            };

            await secretRepository.SetSecretAsync(secretName, protectedToken, tags, cancellationToken);

            return secretName;
        }

        public async Task UpdateAsync(string secretName, GitHubToken token, CancellationToken cancellationToken)
        {
            string serializedToken = SerializeToken(token);
            string protectedToken = dataProtector.Protect(serializedToken);

            IDictionary<string, string> tags = new Dictionary<string, string>
            {
                { "updatedAt", dateTimeProvider.UtcNow.ToString("O") }
            };

            await secretRepository.UpdateSecretAsync(secretName, protectedToken, tags, cancellationToken);
        }

        public async Task<GitHubToken?> GetAsync(string secretName, CancellationToken cancellationToken)
        {
            string? protectedToken = await secretRepository.GetSecretAsync(secretName, cancellationToken);

            if (string.IsNullOrWhiteSpace(protectedToken))
            {
                return null;
            }

            string serializedToken = dataProtector.Unprotect(protectedToken);
            StoredGitHubToken? storedToken = JsonSerializer.Deserialize<StoredGitHubToken>(serializedToken, serializerOptions);

            if (storedToken == null)
            {
                return null;
            }

            DateTimeOffset issuedAt = DateTimeOffset.Parse(storedToken.IssuedAt);
            DateTimeOffset? expiresAt = storedToken.ExpiresAt == null ? null : DateTimeOffset.Parse(storedToken.ExpiresAt);

            GitHubToken gitHubToken = new GitHubToken(
                storedToken.AccessToken,
                storedToken.RefreshToken,
                issuedAt,
                expiresAt,
                storedToken.Scopes);

            return gitHubToken;
        }

        private string BuildSecretName(Guid userId)
        {
            DateTimeOffset now = dateTimeProvider.UtcNow;
            return string.Concat(options.SecretNamePrefix, userId.ToString("N"), "-", now.ToUnixTimeSeconds());
        }

        private string SerializeToken(GitHubToken token)
        {
            StoredGitHubToken storedToken = new StoredGitHubToken
            {
                AccessToken = token.AccessToken,
                RefreshToken = token.RefreshToken,
                IssuedAt = token.IssuedAt.ToString("O"),
                ExpiresAt = token.ExpiresAt.HasValue ? token.ExpiresAt.Value.ToString("O") : null,
                Scopes = token.Scopes.ToList()
            };

            return JsonSerializer.Serialize(storedToken, serializerOptions);
        }

        private sealed class StoredGitHubToken
        {
            public string AccessToken { get; set; } = string.Empty;

            public string RefreshToken { get; set; } = string.Empty;

            public string IssuedAt { get; set; } = string.Empty;

            public string? ExpiresAt { get; set; }

            public IList<string> Scopes { get; set; } = new List<string>();
        }
    }
}
