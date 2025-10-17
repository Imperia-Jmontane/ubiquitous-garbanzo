using System;

namespace MyApp.Application.GitHubOAuth.Models
{
    public sealed class GitHubOAuthClientCredentials
    {
        public GitHubOAuthClientCredentials(string clientId, string clientSecret)
        {
            if (string.IsNullOrWhiteSpace(clientId))
            {
                throw new ArgumentException("The client identifier cannot be null or whitespace.", nameof(clientId));
            }

            if (string.IsNullOrWhiteSpace(clientSecret))
            {
                throw new ArgumentException("The client secret cannot be null or whitespace.", nameof(clientSecret));
            }

            ClientId = clientId;
            ClientSecret = clientSecret;
        }

        public string ClientId { get; }

        public string ClientSecret { get; }
    }
}
