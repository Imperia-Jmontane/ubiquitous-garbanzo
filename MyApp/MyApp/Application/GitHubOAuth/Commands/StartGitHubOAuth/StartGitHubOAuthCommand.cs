using System;
using MediatR;
using MyApp.Application.GitHubOAuth.DTOs;

namespace MyApp.Application.GitHubOAuth.Commands.StartGitHubOAuth
{
    public sealed class StartGitHubOAuthCommand : IRequest<StartGitHubOAuthResultDto>
    {
        public StartGitHubOAuthCommand(Guid userId, string redirectUri)
        {
            if (userId == Guid.Empty)
            {
                throw new ArgumentException("The user identifier cannot be empty.", nameof(userId));
            }

            if (string.IsNullOrWhiteSpace(redirectUri))
            {
                throw new ArgumentException("The redirect URI cannot be null or whitespace.", nameof(redirectUri));
            }

            UserId = userId;
            RedirectUri = redirectUri;
        }

        public Guid UserId { get; }

        public string RedirectUri { get; }
    }
}
