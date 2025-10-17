using System;

namespace MyApp.Application.GitHubOAuth.Models
{
    public sealed class GitHubTokenRefreshRequest
    {
        public GitHubTokenRefreshRequest(string refreshToken)
        {
            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                throw new ArgumentException("The refresh token cannot be null or whitespace.", nameof(refreshToken));
            }

            RefreshToken = refreshToken;
        }

        public string RefreshToken { get; }
    }
}
