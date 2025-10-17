using System;

namespace MyApp.Application.GitHubOAuth.Models
{
    public sealed class GitHubCodeExchangeRequest
    {
        public GitHubCodeExchangeRequest(string code, string redirectUri, string state)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                throw new ArgumentException("The authorization code cannot be null or whitespace.", nameof(code));
            }

            if (string.IsNullOrWhiteSpace(redirectUri))
            {
                throw new ArgumentException("The redirect URI cannot be null or whitespace.", nameof(redirectUri));
            }

            if (string.IsNullOrWhiteSpace(state))
            {
                throw new ArgumentException("The state value cannot be null or whitespace.", nameof(state));
            }

            Code = code;
            RedirectUri = redirectUri;
            State = state;
        }

        public string Code { get; }

        public string RedirectUri { get; }

        public string State { get; }
    }
}
