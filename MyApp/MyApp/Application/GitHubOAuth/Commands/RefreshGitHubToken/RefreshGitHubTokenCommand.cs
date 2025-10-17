using System;
using MediatR;
using MyApp.Application.GitHubOAuth.DTOs;

namespace MyApp.Application.GitHubOAuth.Commands.RefreshGitHubToken
{
    public sealed class RefreshGitHubTokenCommand : IRequest<RefreshGitHubTokenResultDto>
    {
        public RefreshGitHubTokenCommand(Guid userId, string state, string redirectUri)
        {
            if (userId == Guid.Empty)
            {
                throw new ArgumentException("The user identifier cannot be empty.", nameof(userId));
            }

            if (string.IsNullOrWhiteSpace(state))
            {
                throw new ArgumentException("The state value cannot be null or whitespace.", nameof(state));
            }

            if (string.IsNullOrWhiteSpace(redirectUri))
            {
                throw new ArgumentException("The redirect URI cannot be null or whitespace.", nameof(redirectUri));
            }

            UserId = userId;
            State = state;
            RedirectUri = redirectUri;
        }

        public Guid UserId { get; }

        public string State { get; }

        public string RedirectUri { get; }
    }
}
